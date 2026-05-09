namespace App.AOT.Networking.Auth
{
    /// <summary>
    /// 认证上下文：管理token、签名等
    /// </summary>
    public class AuthContext
    {
        public string UserId { get; set; }
        public string Token { get; set; }
        public string Secret { get; set; } // 用于签名
        public long TokenExpireTime { get; set; }

        /// <summary>
        /// 检查token是否有效
        /// </summary>
        public bool IsTokenValid()
        {
            if (string.IsNullOrEmpty(Token))
            {
                return false;
            }

            // TODO: 检查过期时间
            return true;
        }
    }
}
