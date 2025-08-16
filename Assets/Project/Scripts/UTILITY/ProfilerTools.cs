using System.Diagnostics;

namespace Project.Scripts.UTILITY
{
    using Unity.Profiling;
    using System;

    public struct ProfilerHelper : IDisposable
    {
        private ProfilerMarker marker;

        public ProfilerHelper(string name)
        {
            marker = new ProfilerMarker(name);
            marker.Begin();
        }

        public void Dispose()
        {
            marker.End();
        }
    }
    
    
    //usage:
    // void SomeFunction()
    // {
    //     using (new ProfilerHelper("marker 1")) { DoThing1(); }
    //     using (new ProfilerHelper("marker 2")) { DoThing2(); }
    //     using (new ProfilerHelper("marker 3")) { DoThing3(); }
    // }


    public struct TimerHelper : IDisposable
    {
        private Stopwatch stopwatch;
        private string name;

        public TimerHelper(string name)
        {
            this.name = name;
            stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{name} took {stopwatch.Elapsed.TotalMilliseconds} ms");
        }
    }

    // void SomeFunction()
    // {
    //     using (new TimerHelper("Step 1")) { DoThing1(); }
    //     using (new TimerHelper("Step 2")) { DoThing2(); }
    //     using (new TimerHelper("Step 3")) { DoThing3(); }
    // }

}