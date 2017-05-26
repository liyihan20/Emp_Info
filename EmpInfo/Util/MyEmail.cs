using System;
using System.Web.Helpers;

namespace EmpInfo.Util
{
    public class MyEmail
    {
        static string semiServer = "smtp.truly.cn";
        //static string adminEmail = "liyihan.ic@truly.com.cn";

        //发送邮件的包装方法，因为集团邮箱经常发送不出邮件，所以以后默认使用半导体邮箱。2013-6-7
        public static bool SendEmail(string subject, string content, string emailAddress)
        {
            try
            {
                WebMail.SmtpServer = semiServer;
                WebMail.SmtpPort = 25;
                WebMail.UserName = "crm";
                WebMail.From = "\"信利信息查询系统\"<crm@truly.cn>";
                WebMail.Password = "tic3006";
                WebMail.Send(
                to: emailAddress,
                    //bcc: semiBcc,
                subject: subject,
                body: content + "<br /><div style='clear:both'><hr />来自:信利员工信息查询系统<br />注意:此邮件是系统自动发送，请不要直接回复此邮件</div>",
                isBodyHtml: true
                );
            }
            catch
            {
                //发送失败          
                return false;
            }
            return true;
        }

        public static bool SendValidateCode(string code,string emailAddress, string username)
        {
            string subject = "员工邮箱验证";
            string content = "<div>"+username+",你好：</div>";
            content += "<div style='margin-left:30px;'>有用户在信利员工信息系统中发起了对你邮箱的验证操作，如果不是你本人操作，请忽略此邮件。<br />";
            content += "邮箱的验证码是： <span style='font-weight:bold'>"+code+"</span> ，请复制并粘贴到验证文本框中完成验证。</div>";
            return SendEmail(subject, content, emailAddress);
        }

    }
}