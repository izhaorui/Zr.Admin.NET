using System;
using System.Threading;

namespace Infrastructure
{
    /// <summary>
    /// 当前异步流租户上下文。
    /// </summary>
    public static class TenantContext
    {
        private static readonly AsyncLocal<string> _currentTenantId = new();

        /// <summary>
        /// 当前租户ID。
        /// </summary>
        public static string CurrentTenantId
        {
            get => _currentTenantId.Value;
            set => _currentTenantId.Value = value;
        }

        /// <summary>
        /// 在 using 作用域内切换租户，退出时自动恢复。
        /// </summary>
        /// <param name="tenantId">目标租户ID</param>
        /// <returns></returns>
        public static IDisposable Change(string tenantId)
        {
            var previous = CurrentTenantId;
            CurrentTenantId = tenantId;
            return new TenantScope(previous);
        }

        private sealed class TenantScope : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            public TenantScope(string previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                CurrentTenantId = _previous;
                _disposed = true;
            }
        }
    }
}
