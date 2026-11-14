using System;
using AnimalCafe.CafeLoop;
using UnityEngine;

namespace AnimalCafe.BusinessDay
{
    public sealed class BusinessDayController:MonoBehaviour
    {
        [SerializeField] private CafeLoopController loop;
        [SerializeField] private double openSeconds=28800;
        [SerializeField] private double restSeconds=7200;
        [SerializeField] private bool validationMode;
        private BusinessDayService service;
        private Func<double> readNow;
        public event Action<BusinessDaySnapshot> SnapshotChanged;
        public BusinessDaySnapshot Snapshot => service?.GetSnapshot(readNow());
        public bool IsValidationMode => validationMode;
        public CafeServicePresentation ServicePresentation => loop==null?default:loop.ReadServicePresentation();

        public void Initialize(CafeLoopController loop,BusinessDaySettings settings,Func<double> readNow=null,bool validationMode=false)
        {
            if(loop==null) throw new ArgumentNullException(nameof(loop));
            if(settings==null) throw new ArgumentNullException(nameof(settings));
            if(service!=null)
            {
                if(this.loop!=loop) throw new InvalidOperationException("不能替换活动营业日的Cafe Loop。");
                if(isActiveAndEnabled) loop.RegisterBusinessDay(this);
                return;
            }
            this.loop=loop; this.readNow=readNow ?? (()=>Time.realtimeSinceStartupAsDouble);
            this.validationMode=validationMode;
            service=new BusinessDayService(settings,this.readNow());
            if(isActiveAndEnabled) loop.RegisterBusinessDay(this);
        }
        public bool CanAdmitNow() => isActiveAndEnabled && loop!=null && loop.isActiveAndEnabled &&
            service!=null && service.CanAdmit(readNow());
        public void RecordLayoutInterruption(long layoutVersion)
        {
            if(service==null) throw new InvalidOperationException("Business Day recorder尚未初始化。");
            // disabled只撤销driver与admission；同一Service仍须保存canonical layout事实。
            // 不在这里发布SnapshotChanged，避免同步listener重入清场；由唯一driver正常观察。
            service.RecordLayoutInterruption(layoutVersion);
        }
        public void BeforeService()
        {
            if(service==null || !isActiveAndEnabled) return;
            // 时钟读数异常也先关门；失败不会留下driver上帧缓存的允许状态。
            loop.SetAdmissionEnabled(false);
            var now=readNow(); service.BeforeService(now);
            loop.SetAdmissionEnabled(service.CanAdmit(now));
            var value=service.GetSnapshot(now);
            if(value.Phase==BusinessDayPhase.Closing)loop.CancelUnbornAdmissionForClosing();
            SnapshotChanged?.Invoke(value);
        }
        public void AfterService(BusinessDayDrainResult drain)
        {
            if(service==null || !isActiveAndEnabled) return;
            var now=readNow(); service.AfterService(now,drain);
            loop.SetAdmissionEnabled(service.CanAdmit(now));
            SnapshotChanged?.Invoke(service.GetSnapshot(now));
        }
        private void Start()
        {
            // builder只设置serialized refs；仅PlayMode启动现实时间计时。
            if(service==null && loop!=null) Initialize(loop,new BusinessDaySettings(openSeconds,restSeconds),validationMode:validationMode);
        }
        private void OnEnable()
        { if(service!=null && loop!=null) loop.RegisterBusinessDay(this); }
        private void OnDisable() => loop?.UnregisterBusinessDay(this);
        private void OnDestroy()
        {
            loop?.UnregisterBusinessDay(this);
            loop?.ReleaseBusinessDayRecorder(this);
        }
    }
}
