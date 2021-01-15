using System;

namespace ParallelAsync
{
    public class ParallelizeOption
    {
        public int MaxDegreeOfParallelism { get; set; }
        public Fail FailMode { get; set; }
    }

    public class ParallelMonitor<T>
    {
        public ParallelMonitor(int optionMaxDegreeOfParallelism)
        {
            if (optionMaxDegreeOfParallelism <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(optionMaxDegreeOfParallelism));
            }
            _activeItem = new T[optionMaxDegreeOfParallelism];
        }

        public void SetActive(int index, T item)
        {
            _activeItem[index] = item;
        }
        public void SetInactive(int index)
        {
            _activeItem[index] = default;
        }

        private readonly T[] _activeItem;
    }
}