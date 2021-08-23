using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Constant
{
    public class HttpStatus
    {
        /// <summary>
        /// 操作成功
        /// </summary>
        public static readonly int SUCCESS = 200;
        /// <summary>
        /// 对象创建成功
        /// </summary>
        public static readonly int CREATED = 201;

        /// <summary>
        /// 请求已经被接受
        /// </summary>
        public static readonly int ACCEPTED = 202;

        /// <summary>
        /// 操作已经执行成功，但是没有返回数据
        /// </summary>
        public static readonly int NO_CONTENT = 204;

        /// <summary>
        /// 资源已被移除
        /// </summary>
        public static readonly int MOVED_PERM = 301;

        /// <summary>
        /// 重定向
        /// </summary>
        public static readonly int SEE_OTHER = 303;

        /// <summary>
        /// 资源没有被修改
        /// </summary>
        public static readonly int NOT_MODIFIED = 304;

        /**
         * 参数列表错误（缺少，格式不匹配）
         */
        public static readonly int BAD_REQUEST = 400;

        /// <summary>
        /// 未授权
        /// </summary>
        public static readonly int UNAUTHORIZED = 401;

        /**
         * 访问受限，授权过期
         */
        public static readonly int FORBIDDEN = 403;

        /**
         * 资源，服务未找到
         */
        public static readonly int NOT_FOUND = 404;

        /**
         * 不允许的http方法
         */
        public static readonly int BAD_METHOD = 405;

        /**
         * 资源冲突，或者资源被锁
         */
        public static readonly int CONFLICT = 409;

        /**
         * 不支持的数据，媒体类型
         */
        public static readonly int UNSUPPORTED_TYPE = 415;

        /**
         * 系统内部错误
         */
        public static readonly int ERROR = 500;

        /**
         * 接口未实现
         */
        public static readonly int NOT_IMPLEMENTED = 501;
    }
}
