using DTSoft.Models.Entities;

namespace DTSoft.Models.Parameter.User
{
	public class OnlineUser : SysUser
	{
		//消息标识
		public required string ConnectionId { get; set; }
	}
}

