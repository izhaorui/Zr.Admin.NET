namespace ZR.Model.Dto
{
    public class ChatMessageDto
    {
        public ChatUserDto FromUser { get; set; }
        public bool Self { get; set; }
        [SugarColumn(IsPrimaryKey = true)]
        public string ChatId { get; set; }
        /// <summary>
        /// 0、文本 1、图片 2、文件 3、语音 4、视频
        /// </summary>
        public int MsgType { get; set; }
        public string StoredKey { get; set; }
        public long UserId { get; set; }
        public long ToUserId { get; set; }

        /// <summary>
        /// 消息内容，如果type=1/2/3/4，此属性表示文件的URL地址
        /// </summary>
        public string Message { get; set; }
        public long ChatTime { get; set; }
        public int Online { get; set; }
    }

    public class ChatUserDto
    {
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Avatar { get; set; }
    }
}
