using System;
using System.Web.Helpers;

namespace EmpInfo.Util
{
    public class MyEmail
    {
        public static bool SendValidateCode(string code,string emailAddress, string username)
        {
            string subject = "员工邮箱验证";
            string content = "<div>"+username+",你好：</div>";
            content += "<div style='margin-left:30px;'>有用户在信利员工信息系统中发起了对你邮箱的验证操作，如果不是你本人操作，请忽略此邮件。<br />";
            content += "邮箱的验证码是： <span style='font-weight:bold'>"+code+"</span> ，请复制并粘贴到验证文本框中完成验证。</div>";

            //直接调用封装在dll的邮件发送函数
            return TrulyEmail.EmailUtil.SemiSend("信利员工信息查询系统", subject, content, emailAddress);
            
        }

        public static bool SendEmail(string subject, string emailAddrs, string content)
        {
            return TrulyEmail.EmailUtil.SemiSend("信利员工信息查询系统", subject, content, emailAddrs);            
        }


    }
}