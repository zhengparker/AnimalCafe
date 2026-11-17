using System;
using System.IO;
using System.Linq;

namespace AnimalCafe.Tests.EditMode
{
    internal static class TestAssetBytes
    {
        internal static void Restore(string path, byte[] bytes)
        {
            var target = Path.GetFullPath(path);
            var existed = File.Exists(target);
            if (existed && File.ReadAllBytes(target).SequenceEqual(bytes)) return;

            // Never truncate a mapped asset. 先关闭独有临时文件，再原子发布 bytes。
            var temporary = Path.Combine(Path.GetDirectoryName(target),
                "." + Path.GetFileName(target) + ".testrestore-" + Guid.NewGuid().ToString("N") + ".tmp");
            var ownsTemporary = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsTemporary = true;
                    stream.Write(bytes, 0, bytes.Length);
                }
                if (existed) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally
            {
                // A failed CreateNew never grants ownership. 不清理其他调用方的文件。
                if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
