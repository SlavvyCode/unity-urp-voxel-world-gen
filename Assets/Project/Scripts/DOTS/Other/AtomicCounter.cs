using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Project.Scripts.DOTS.Other
{
 
    [BurstCompile]
    public struct AtomicCounter
    {
        private NativeArray<int> counter;

        public AtomicCounter(Allocator allocator)
        {
            counter = new NativeArray<int>(1, allocator);
            counter[0] = 0;
        }

        public void Dispose()
        {
            if (counter.IsCreated) counter.Dispose();
        }

        public int Add(int value)
        {
            // Use Interlocked.Add for atomic addition
            unsafe
            {
                int* ptr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counter);
                return Interlocked.Add(ref *ptr, value) - value;
            }
        }

        public int Value
        {
            get => counter[0];
            set => counter[0] = value;
        }
    
    
        public void Reset()
        {
            if (counter.IsCreated)
            {
                counter[0] = 0;
            }
        }
    }
}