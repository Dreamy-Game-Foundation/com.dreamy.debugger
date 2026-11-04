
namespace Dreamy.Debugger.Presentation.Services
{
    using System;
    using Profiler;
    using Dreamy.Debugger.Foundation.Service;
#if UNITY_2018_1_OR_NEWER
    using UnityEngine.Rendering;
    using UnityEngine.Experimental.Rendering;
#endif

    public static class ProfilerServiceSelector
    {
        [ServiceSelector(typeof(IProfilerService))]
        public static Type GetProfilerServiceType() => typeof(ProfilerServiceImpl);
    }

    public struct ProfilerFrame
    {
        public double FrameTime;
        public double OtherTime;
        public double RenderTime;
        public double UpdateTime;
    }

    public interface IProfilerService
    {
        float AverageFrameTime { get; }
        float LastFrameTime { get; }
        CircularBuffer<ProfilerFrame> FrameBuffer { get; }
    }
}
