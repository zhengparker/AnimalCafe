#import <UIKit/UIKit.h>
#import <UnityFramework/UnityFramework.h>

// Use the actual Unity view, including iPad window sizing and display zoom.
// 使用 Unity view 的 points，避免把设备 DPI 或渲染像素误认为 iOS pt。
// Unity's public sample uses this same appController/rootView access:
// https://github.com/Unity-Technologies/uaal-example/blob/master/NativeiOSApp/NativeiOSApp/MainViewController.mm
extern "C" int P8R_GetUnityViewportPoints(float* width, float* height)
{
    if (width == nullptr || height == nullptr) return 0;
    *width = 0;
    *height = 0;
    // Unity calls from its main thread; do not read UIKit from a worker or block dispatch.
    // UIKit 只能从 main thread 读取；初始化未就绪时交给 C# 的有界重试。
    if (![NSThread isMainThread]) return 0;
    UIView* view = [[[UnityFramework getInstance] appController] rootView];
    if (view == nil || view.window == nil) return 0;
    const CGSize points = view.bounds.size;
    if (points.width <= 0 || points.height <= 0) return 0;
    *width = (float)points.width;
    *height = (float)points.height;
    return 1;
}
