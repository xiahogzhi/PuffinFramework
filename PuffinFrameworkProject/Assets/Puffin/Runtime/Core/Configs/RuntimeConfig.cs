using System;
using System.Collections.Generic;

namespace Puffin.Runtime.Core.Configs
{
    /// <summary>
    /// Runtime 配置
    /// </summary>
    public class RuntimeConfig
    {
        /// <summary>
        /// 是否启用性能统计
        /// </summary>
        public bool EnableProfiling { get; set; }

        /// <summary>
        /// 预定义的条件符号
        /// </summary>
        public List<string> Symbols { get; set; } = new();

        /// <summary>
        /// 手动指定要注册的系统类型（优先于扫描）
        /// </summary>
        public List<Type> ManualSystemTypes { get; set; } = new();
    }
}
