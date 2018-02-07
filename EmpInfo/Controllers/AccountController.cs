using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.EmpWebSvr;

namespace EmpInfo.Controllers
{
    public class AccountController : BaseController
    {
        public ActionResult NoPowerToVisit(string controlerName, string actionName)
        {
            WriteEventLog("不能访问", controlerName + "/" + actionName, 1000);
            return View();
        }

        [AllowAnonymous]
        public ActionResult Login(string from = "")
        {
            TrulyEmpSvrSoapClient client = new TrulyEmpSvrSoapClient();
            string appUrl = client.GetAppUrl();
            ViewData["appUrl"] = appUrl;
            Session["fromwx"] = "wx".Equals(from) ? true : false;
            return View();
        }

        [AllowAnonymous]
        public ActionResult getImage()
        {
            string code = MyUtils.CreateValidateNumber(4);
            Session["code"] = code.ToLower();
            byte[] bytes = MyUtils.CreateValidateGraphic(code);
            return File(bytes, @"image/jpeg");
        }

        [AllowAnonymous]
        [HttpPost]
        public JsonResult Login(FormCollection fc)
        {
            LoginModel model = new LoginModel()
            {
                UserName = fc.Get("username"),
                Password = fc.Get("password"),
                VailidateCode = fc.Get("code"),
                rememberDay = Int32.Parse(fc.Get("rememberDay")),
                UserPlatform = fc.Get("userPlatform"),
                UserAgent = fc.Get("userAgent")
            };
            var result = StartLogin(model);
            return Json(result);
        }

        [AllowAnonymous]
        private SimpleResultModel StartLogin(LoginModel model)
        {
            WriteEventLogWithoutLogin(model.UserName, "platform:" + model.UserPlatform + ";agent:" + model.UserAgent);

            if (!model.VailidateCode.ToLower().Equals((string)Session["code"]))
                return new SimpleResultModel() { suc = false, msg = "验证码错误" };
            int maxFailTimes = 5;
            string md5Password = MyUtils.getMD5(model.Password);
            bool suc = false;
            string msg = "";
            string errorMsg = "";
            DateTime lastSixMonth = DateTime.Now.AddDays(-180);
            ei_users thisUser;
            
            var user = db.ei_users.Where(u => u.card_number == model.UserName.Trim());
            if (user.Count() < 1)
            {
                msg = "用户名不存在，登陆失败!";
            }
            else if (user.Where(u => u.forbit_flag == true).Count() > 0)
            {
                msg = "用户名已被禁用，登陆失败!";
            }
            else if (db.GetHREmpStatus(model.UserName).Count() == 0 || (db.GetHREmpStatus(model.UserName).Count()>0 && db.GetHREmpStatus(model.UserName).ToList().First() != "在职"))
            {
                thisUser = user.First();
                thisUser.forbit_flag = true;
                msg = "你在人事系统不是【在职】状态，不能登陆";
            }
            else if (user.Where(u => u.last_login_date != null && u.last_login_date < lastSixMonth).Count() > 0)
            {
                thisUser = user.First();
                thisUser.forbit_flag = true;
                msg = "连续六个月未登录，被系统禁用";
            }
            else if (user.Where(u => u.password == md5Password).Count() < 1)
            {
                thisUser = user.First();
                if (thisUser.fail_times == null)
                    thisUser.fail_times = 1;
                else
                    thisUser.fail_times = thisUser.fail_times + 1;

                if (thisUser.fail_times >= maxFailTimes)
                {
                    thisUser.forbit_flag = true;
                    thisUser.fail_times = 0;
                    msg = "连续" + maxFailTimes + "次密码错误，用户被禁用!";
                }
                else
                {
                    msg = "已连续" + thisUser.fail_times + "次密码错误，你还剩下" + (maxFailTimes - thisUser.fail_times) + "次尝试机会。";
                }
                errorMsg = "密码错误：" + model.Password + ";" + msg;
            }
            else
            {
                //成功登录
                    thisUser = user.First();
                    thisUser.fail_times = 0;
                    thisUser.last_login_date = DateTime.Now;
                    
                    //写入cookie
                    setcookie(thisUser, model.rememberDay);

                    msg = "登陆成功";
                    suc = true;
                
            }
            if (suc)
            {
                //验证密码复杂性
                string passwordInfo = MyUtils.ValidatePassword(model.Password);
                if (!string.IsNullOrEmpty(passwordInfo)) {
                    WriteEventLog("用户登陆", "复杂性不足，强制修改密码");
                    msg = "ResetPassword";
                }
                WriteEventLog("用户登录", msg);                
            }
            else
            {
                WriteEventLogWithoutLogin(model.UserName, string.IsNullOrEmpty(errorMsg) ? msg : errorMsg, -10);
            }
            return new SimpleResultModel() { suc = suc, msg = msg };
        }

        //登录页面的重置密码
        public JsonResult ResetPassword(string card_no, string newPass)
        {
            string passTip = MyUtils.ValidatePassword(newPass);
            if (string.IsNullOrEmpty(passTip)) {
                var users = db.ei_users.Where(u => u.card_number == card_no);
                if (users.Count() != 1) {
                    return Json(new SimpleResultModel() { suc = false, msg = "你在此系统没有用户，请先注册" });
                }
                else {
                    var user = users.First();
                    user.password = MyUtils.getMD5(newPass);
                    db.SaveChanges();
                    WriteEventLogWithoutLogin(card_no, "修改成复杂密码");
                    return Json(new SimpleResultModel() { suc = true, msg = "密码修改成功，请重新登陆系统" });
                }
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = passTip });
            }
        }

        public ActionResult LogOut()
        {
            var cookie = new HttpCookie(ConfigurationManager.AppSettings["cookieName"]);
            cookie.Expires = DateTime.Now.AddDays(-1);
            Response.AppendCookie(cookie);
            Session.Clear();
            return RedirectToAction("Login");
        }

        //注册模块，第一步，输入厂牌，获得姓名
        public JsonResult ValidateCardNumber(string card_no)
        {
            if (db.ei_users.Where(u => u.card_number == card_no).Count() > 0)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "该用户已经注册，不能重复注册！" });
            }
            //判断是否特殊用户
            if (db.ei_specialUsers.Where(s => s.card_no == card_no).Count() > 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "此厂牌处于特殊保护状态，要注册请与管理员联系。谢谢！" });
            }

            var infos = db.GetHREmpInfo(card_no).ToList();
            if (infos.Count() < 1)
            {
                WriteEventLogWithoutLogin(card_no, "用户注册[第一步]:厂牌编号不存在");
                return Json(new { suc = false, msg = "厂牌编号不存在" });
            }
            else if (string.IsNullOrEmpty(infos.First().txm)) {
                WriteEventLogWithoutLogin(card_no, "工资账号还未生效");
                return Json(new { suc = false, msg = "用户注册[第一步]:工资账号未生效，请过几天再注册" });
            }
            else
            {
                WriteEventLogWithoutLogin(card_no, "用户注册[第一步]:通过");
                return Json(new { suc = true });
            }
        }

        //注册模块，第二步，输入姓名和身份证号码后6位进行验证，获取邮箱地址和电话号码前7位
        public JsonResult ValidateIDNumber(string card_no, string id_number,string name)
        {
            var infos = db.GetHREmpInfo(card_no).ToList();
            if (infos.Count() < 1)
            {
                return Json(new { suc = false, msg = "厂牌编号不存在" });
            }
            else
            {
                var info = infos.First();
                if (!info.emp_name.Equals(name))
                {
                    return Json(new { suc = false, msg = "姓名不正确" });
                }
                if (info.id_code.EndsWith(id_number.Trim()))
                {
                    string email = !string.IsNullOrWhiteSpace(info.email) && info.email.Contains(@"@") ? info.email : "";
                    string phone = !string.IsNullOrWhiteSpace(info.tp) && info.tp.Length == 11 ? info.tp.Substring(0, 3) + " " + info.tp.Substring(3, 4) : "";
                    WriteEventLogWithoutLogin(card_no, "用户注册[第二步]：通过");
                    return Json(new { suc = true, email = email, phone = phone });
                }
                else
                {
                    WriteEventLogWithoutLogin(card_no, "用户注册[第二步]:身份证后6位验证出错");
                    return Json(new { suc = false, msg = "身份证后6位验证出错" });
                }
            }
        }

        //发送邮箱验证邮件
        public JsonResult SendValidateEmail(string name,string email)
        {
            string code = MyUtils.CreateValidateNumber(6);
            Session["emailCode"] = code.ToLower();
            if (MyEmail.SendValidateCode(code, email, name))
            {
                WriteEventLogWithoutLogin(name, "用户注册,验证邮件发送成功:" + email);
                return Json(new SimpleResultModel() { suc = true, msg = "验证邮件发送成功" });
            }
            else
            {
                WriteEventLogWithoutLogin(name, "用户注册,验证邮件发送失败:" + email);
                return Json(new SimpleResultModel() { suc = false, msg = "验证邮件发送失败，请稍候再试，如果还是不行，请联系系统管理员" });
            }
        }

        //验证邮箱发送的验证码
        public bool ValidateEmailCode(string email_code)
        {
            if (Session["emailCode"] != null && email_code.Trim().ToLower().Equals((string)Session["emailCode"])) {
                return true;
            }else{
                return false;
            }

        }

        //注册第三步，根据电话号码后4位或邮箱验证码验证，如通过，进入第四部，输出完整身份证号码
        public JsonResult ValidatePhoneAndEmail(string card_no,string phone_number, string email_code)
        {
            bool pass = false;
            string msg = "";
            var infos = db.GetHREmpInfo(card_no).ToList();
            if (infos.Count() < 1)
            {
                return Json(new { suc = false, msg = "厂牌编号不存在" });
            }
            var info = infos.First();
            if (!string.IsNullOrWhiteSpace(phone_number))
            {
                if (!string.IsNullOrWhiteSpace(info.tp) && info.tp.EndsWith(phone_number))
                {
                    pass = true;
                }
                else
                {
                    pass = false;
                    msg = "电话号码后4位不正确，验证失败";
                }
            }
            if (!string.IsNullOrWhiteSpace(email_code))
            {
                if (ValidateEmailCode(email_code))
                {
                    pass = true;
                }
                else
                {
                    pass = false;
                    msg = "邮箱验证码输入不正确，验证失败";
                }
            }
            WriteEventLogWithoutLogin(card_no, "注册第三步：通过与否：" + pass.ToString() + ",msg:" + msg);
            if (pass)
            {
                return Json(new { suc = true, id_number = info.id_code });
            }
            else
            {
                return Json(new { suc = false, msg = msg });
            }

        }

        //完成注册，保存用户信息
        public JsonResult FinishRegister(FormCollection fc)
        {
            var card_no = fc.Get("card_no");
            var password = fc.Get("password");
            //验证该用户是否已经存在
            if (db.ei_users.Where(u => u.card_number == card_no).Count() > 0)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "该用户已经注册，不能重复注册！" });
            }
            if (password.Length < 6)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "密码长度必须不少于6个字符" });
            }
            string passTip = MyUtils.ValidatePassword(password);
            if (!string.IsNullOrEmpty(passTip)) {
                return Json(new SimpleResultModel() { suc = false, msg = passTip });
            }
            try
            {
                var empInfo = db.GetHREmpInfo(card_no).First();
                ei_users user = new ei_users()
                {
                    card_number = card_no,
                    name = empInfo.emp_name,
                    email = empInfo.email,
                    id_number = empInfo.id_code,
                    sex = empInfo.sex,
                    phone = empInfo.tp,
                    //portrait = empInfo.zp,
                    short_portrait = empInfo.zp == null ? null : MyUtils.MakeThumbnail(MyUtils.BytesToImage(empInfo.zp)),
                    salary_no = empInfo.txm,
                    create_date = DateTime.Now,
                    fail_times = 0,
                    forbit_flag = false,
                    password = MyUtils.getMD5(password)
                };
                db.ei_users.Add(user);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLogWithoutLogin("注册", "发生错误：" + ex.Message, -1000);
                return Json(new SimpleResultModel() { suc = false, msg = "系统发生错误，请联系管理员处理" });
            }
            WriteEventLogWithoutLogin("注册", "自助注册成功");
            return Json(new SimpleResultModel() { suc = true, msg = "注册成功，请登录系统" });            
        }

        //更新照片，无照片的去取人事系统的
        public string UpdatePortrait()
        {
            var list = db.ei_emp_portrait.ToList();
            foreach (var l in list) {
                var emp = db.ei_users.Single(e => e.card_number == l.card_number);
                emp.short_portrait = MyUtils.MakeThumbnail(MyUtils.BytesToImage(l.zp));                
            }
            db.SaveChanges();
            return "ok";
        }

        //用户是否有登记邮箱
        public JsonResult UserHasEmail(string card_no)
        {
            var user = db.ei_users.Where(u => u.card_number == card_no);
            if (user.Count() != 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "你在此系统没有用户，请先注册" });
            }
            else {
                string email = user.First().email;
                if (!string.IsNullOrWhiteSpace(email) && email.Contains("@")) {
                    return Json(new SimpleResultModel() { suc = true, extra = email });
                }
            }            
            WriteEventLogWithoutLogin(card_no, "重置密码-没有邮箱", -1);
            return Json(new SimpleResultModel() { suc = false, msg = "你没有在此系统登记邮箱，不能自助解禁或重置密码；请将【厂牌】、【姓名】和【身份证号码】(三者缺一不可，否则不予处理)发给系统管理员处理！" });
        }

        //验证邮箱验证码与身份证后6位是否正确,如果正确可以解禁或者重置密码
        public JsonResult ResetValidate(string card_no, string email_code, string idNumber, string opType)
        {
            var users = db.ei_users.Where(u => u.card_number == card_no);
            if (users.Count() != 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "你在此系统没有用户，请先注册" });
            }
            else {
                var user = users.First();
                if (!ValidateEmailCode(email_code)) {
                    WriteEventLogWithoutLogin(card_no, "重置密码-邮箱验证失败", -1);
                    return Json(new SimpleResultModel() { suc = false, msg = "邮箱验证失败，请重试" });
                }
                if (!user.id_number.EndsWith(idNumber)) {
                    WriteEventLogWithoutLogin(card_no, "重置密码-身份证验证失败", -1);
                    return Json(new SimpleResultModel() { suc = false, msg = "身份证后六位错误，验证失败" });
                }
                else {
                    //验证成功
                    if (opType.IndexOf("active") >= 0) {
                        user.forbit_flag = false;
                        user.last_login_date = DateTime.Now;
                        db.SaveChanges();
                    }
                    WriteEventLogWithoutLogin(card_no, "重置密码-验证成功；type:" + opType);
                    string okMsg = "";
                    string isReset = "0";
                    switch (opType) {
                        case "reset":
                            okMsg = "验证通过，请重新设置密码";
                            isReset = "1";
                            break;
                        case "active":
                            okMsg = "验证通过，用户已解禁，请重新登陆";
                            break;
                        case "active_reset":
                            okMsg = "验证通过，用户已解禁，请重新设置密码";
                            isReset = "1";
                            break;
                        default:
                            okMsg = "验证通过";
                            break;
                    }
                    return Json(new SimpleResultModel() { suc = true, msg = okMsg, extra = isReset });
                }
            }
        }

    }
}
