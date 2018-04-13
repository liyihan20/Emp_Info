using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{

    public class UserInfo
    {
        public int id { get; set; }
        public string cardNo { get; set; }
        public string name { get; set; }

    }

    public class UserInfoDetail
    {
        public string name { get; set; }
        public string email { get; set; }

        public string shortPhone { get; set; }
        public string phone { get; set; }

        public string idNumber { get; set; }

        public string sex { get; set; }

        public string salaryNo { get; set; }
        public string depNum { get; set; }
        public string depLongName { get; set; }
        public string AesOpenId { get; set; }

    }
    public class LoginModel
    {
        [Required]
        [Display(Name = "用户名")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string Password { get; set; }

        [Display(Name = "记住我的天数")]
        public int rememberDay { get; set; }

        [Required]
        [Display(Name = "验证码")]
        public string VailidateCode { get; set; }

        public string UserPlatform { get; set; }
        public string UserAgent { get; set; }
    }

}