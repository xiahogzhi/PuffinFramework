using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// Update 频率控制 - 每 N 帧执行一次
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UpdateIntervalAttribute : Attribute
    {
        public int FrameInterval { get; }

        public UpdateIntervalAttribute(int frameInterval)
        {
            FrameInterval = Math.Max(1, frameInterval);
        }
    }
}
