using EmpInfo.Filter;
using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;


namespace EmpInfo.Controllers
{    
    public class HomeController : BaseController
    {
        //主页
        [SessionTimeOutFilter]
        public ActionResult Index()
        {
            WriteEventLog("主界面", "打开主页");
            ViewData["username"] = userInfo.name;
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;

            if ("06022701".Equals(userInfo.cardNo)) {
                //审计来，吉全界面调整
                bool auditorComing = bool.Parse(ConfigurationManager.AppSettings["auditorComing"]);
                ViewData["auditorComing"] = auditorComing;
            }
            bool fromWx = Session["fromwx"] == null ? false : (bool)Session["fromwx"];//是否从微信过来
            ViewData["fromWx"] = fromWx; //是否从微信端访问

            var pushUsers = db.vw_push_users.Where(v => v.id == userInfo.id).ToList();
            ViewData["wxSetting"] = pushUsers.Count() > 0 ? "1" : "0"; //是否可以看到微信公众号的有关设置
            
            //查看工资是否需要再次确认密码
            bool checkSalaryNeedPassword = false;
            if (fromWx) {
                if (pushUsers.Count() > 0) {
                    if (pushUsers.First().wx_check_salary_info == false) {
                        checkSalaryNeedPassword = true;
                    }
                }
            }
            ViewData["checkSalaryNeedPassword"] = checkSalaryNeedPassword;

            return View();
        }

        //获取头像
        public ActionResult GetEmpPortrait(string card_no)
        {
            if (string.IsNullOrEmpty(card_no)) card_no = userInfo.cardNo;
            var user = db.ei_users.Where(u => u.card_number == card_no).FirstOrDefault();
            byte[] portrait = user == null ? null : user.short_portrait;
            if (portrait == null) {
                //无照片的，先看看人事系统有没有 2017-9-5
                string picUrl;
                var emp = db.GetHREmpInfo(card_no).ToList();
                if (emp.Count() > 0) {
                    if (emp.First().zp != null) {
                        portrait = MyUtils.MakeThumbnail(MyUtils.BytesToImage(emp.First().zp));
                        if (user != null) {
                            user.short_portrait = portrait;
                            db.SaveChanges();
                        }
                    }
                    else {
                        picUrl = Server.MapPath("~/Content/images/") + (user.sex.Equals("男") ? "user_man.png" : "user_woman.png");
                        portrait = MyUtils.GetServerImage(picUrl);
                    }
                }
                else {
                    picUrl = Server.MapPath("~/Content/images/") + (user.sex.Equals("男") ? "user_man.png" : "user_woman.png");
                    portrait = MyUtils.GetServerImage(picUrl);
                }
            }
            return File(portrait, @"image/bmp");
        }

        //获取二维码
        public ActionResult GetQrCode()
        {
            byte[] code = MyUtils.GetQrCode(userInfo.cardNo);
            return File(code, @"image/jpeg");
        }

        //获取一维码
        public ActionResult GetCode39()
        {
            byte[] code=MyUtils.GetCode39(userInfo.cardNo);
            return File(code, @"image/bmp");
        }


        #region 更新个人信息
        //更新信息之前验证密码
        [SessionTimeOutJsonFilter]
        public JsonResult ValidateOldPassword(string old_pass)
        {
            old_pass = MyUtils.getMD5(old_pass);
            if (db.ei_users.Where(u => u.card_number == userInfo.cardNo && u.password == old_pass).Count() < 1)
            {
                WriteEventLog("主界面", "验证旧密码，错误");
                return Json(new SimpleResultModel() { suc = false, msg = "原始密码错误" });
            }
            WriteEventLog("主界面", "验证旧密码，正确");
            var pushUsers = db.vw_push_users.Where(v => v.id == userInfo.id).ToList();

            bool checkSalaryInfo = false, pushSalaryInfo = false, pushConsumeInfo = false, pushFlowInfo = false;

            if (pushUsers.Count() > 0) {
                var pushUser=pushUsers.First();
                checkSalaryInfo = pushUser.wx_check_salary_info ?? false;
                pushSalaryInfo = pushUser.wx_push_salary_info ?? false;
                pushConsumeInfo = pushUser.wx_push_consume_info ?? false;
                pushFlowInfo = pushUser.wx_push_flow_info ?? false;
            }
            return Json(new
            {
                suc = true,
                phone = userInfoDetail.phone,
                email = userInfoDetail.email,
                shortPhone = userInfoDetail.shortPhone,
                depLongName = userInfoDetail.depLongName,
                depNum = userInfoDetail.depNum,
                checkSalaryInfo = checkSalaryInfo,
                pushSalaryInfo = pushSalaryInfo,
                pushConsumeInfo = pushConsumeInfo,
                pushFlowInfo = pushFlowInfo
            });
        }

        //更新个人信息
        [SessionTimeOutJsonFilter]
        public JsonResult UpdatePersonalInfo(FormCollection fc)
        {
            string phone=fc.Get("phone");
            string email = fc.Get("email");
            string new_pass = fc.Get("new_pass");
            string shortPhone = fc.Get("shortPhone");
            //string depNum = fc.Get("depNum");
            //string depLongName = fc.Get("depLongName");
            string checkSalaryInfo = fc.Get("checkSalaryInfo");
            string pushSalaryInfo = fc.Get("pushSalaryInfo");
            string pushConsumeInfo = fc.Get("pushConsumeInfo");
            string pushFlowInfo = fc.Get("pushFlowInfo");

            var user = db.ei_users.Single(u => u.card_number == userInfo.cardNo);
            if (!string.IsNullOrEmpty(email))
            {
                email = email.Trim();
                var emailR = new Regex(@"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$");
                if (!emailR.IsMatch(email))
                {
                    return Json(new SimpleResultModel() { suc = false, msg = "邮箱地址不合法" });
                }
                if (db.ei_users.Where(u => u.email == email && u.id != userInfo.id && u.name!=userInfo.name).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此邮箱已被其他人绑定" });
                }
                user.email = email;
            }
            if (!string.IsNullOrEmpty(phone))
            {
                var phoneR = new Regex(@"^\d{11}$");
                phone = phone.Trim();
                if (!phoneR.IsMatch(phone))
                {                    
                    return Json(new SimpleResultModel() { suc = false, msg = "手机长号必须是11位数字" });
                }
                if (db.ei_users.Where(u => u.phone == phone && u.id != userInfo.id && u.name != userInfo.name).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此手机长号已被其他人绑定" });
                }
                user.phone = phone;
            }
            user.short_phone = shortPhone;
            if (!string.IsNullOrEmpty(new_pass))
            {
                string passTip = MyUtils.ValidatePassword(new_pass);
                if (string.IsNullOrEmpty(passTip)) {
                    user.password = MyUtils.getMD5(new_pass);
                }
                else {
                    return Json(new SimpleResultModel() { suc = false, msg = passTip });
                }
            }
            
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(shortPhone)) {
                db.InsertIntoYFEmp(email ?? "", phone ?? "", shortPhone ?? "", user.name, user.card_number);
            }
            //user.dep_no = depNum;
            //user.dep_long_name = depLongName;
            user.wx_check_salary_info = bool.Parse(checkSalaryInfo);
            user.wx_push_salary_info = bool.Parse(pushSalaryInfo);
            user.wx_push_consume_info = bool.Parse(pushConsumeInfo);
            user.wx_push_flow_info = bool.Parse(pushFlowInfo);

            db.SaveChanges();
            ClearUserInfoDetail();
            WriteEventLog("主界面", "用户信息更新成功");
            return Json(new SimpleResultModel() { suc = true, msg = "用户信息更新成功" });
        }
        #endregion

        #region 宿舍费用查询
        //查询宿舍费用之前先验证是否有住宿过，最近6个月是否有记录
        [SessionTimeOutJsonFilter]
        public JsonResult ValidateDorm()
        {
            var result = db.ValidateDormStatus(userInfoDetail.salaryNo).ToList();
            if (result.Count() > 0)
            {
                return Json(new SimpleResultModel() { suc = result.First().suc == 1 ? true : false, msg = result.First().msg });
            }
            return Json(new SimpleResultModel() { suc = false, msg = "查询失败" });
        }

        //宿舍费用查询
        [SessionTimeOutFilter]
        public ActionResult CheckDormitory()
        {
            //住宿状态
            var dormInfo = db.GetEmpDormInfo(userInfo.cardNo).ToList();
            
            var model = new DormLivingInfo();
            if (dormInfo.Count() > 0)
            {
                model.livingStatus = "在住";
                model.areaNumber = dormInfo.First().area;
                model.dormNumber = dormInfo.First().dorm_number;
                model.inDate = ((DateTime)dormInfo.First().in_date).ToString("yyyy-MM-dd");
            }
            else
            {
                model.livingStatus = "未住宿";
                model.areaNumber = "无";
                model.dormNumber = "无";
                model.inDate = "无";
            }
            ViewData["dormLivingInfo"] = model;
            //费用年月份
            var yearMonthArr = db.GetDormChargeMonth().ToArray();
            ViewData["yearMonthArr"] = yearMonthArr;

            WriteEventLog("住宿界面", "获取基础住宿信息");
            return View();
        }

        //每月房租水电费等
        [SessionTimeOutJsonFilter]
        public JsonResult GetDormFee(string year_month)
        {
            var fees = db.GetDormFeeByMonth(year_month.Replace("-", ""), userInfoDetail.salaryNo).ToList();
            if (fees.Count() < 1)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "查询不到相关信息" });
            }
            DormFeeModel model = new DormFeeModel();
            model.yearMonth = year_month;
            foreach (var fee in fees)
            {
                model.dormNumber += "  " + fee.dorm_number;
                model.rent += "  " + fee.rent;
                model.management += "  " + fee.management;
                model.elec += "  " + fee.electricity;
                model.water += "  " + fee.water;
                model.fine += "  " + fee.fine;
                model.repair += "  " + fee.repair;
                model.others += "  " + fee.others;
                model.comment += "  " + fee.comment;
            }
            model.total = fees.Sum(f => f.total).ToString();

            WriteEventLog("住宿界面", "获取费用信息：" + year_month);
            return Json(new { suc = true, model = model });
        }

        #endregion

        #region 饭卡查询
        //饭卡查询
        [SessionTimeOutFilter]
        public ActionResult CheckDinningCard()
        {
            var model = dinningCarStatusModel;
            ViewData["CardStatus"] = model;
            WriteEventLog("饭卡查询", "主界面，余额为：" + model.remainingSum);
            return View();
        }

        //消费记录
        [SessionTimeOutJsonFilter]
        public JsonResult GetConsumeRecords(string from_date, string to_date)
        {
            var records = canteenDb.ljq20160323_001(userInfo.cardNo, from_date, to_date).ToList();
            if (records.Count() < 1)
            {
                WriteEventLog("饭卡查询", "消费记录：" + from_date + "~" + to_date + ":此时间段查询不到相关记录");
                return Json(new SimpleResultModel() { suc = false, msg = "此时间段查询不到相关记录" });
            }
            List<DiningCardConsumeRecords> list = new List<DiningCardConsumeRecords>();
            foreach (var r in records.OrderByDescending(r => r.消费时间))
            {
                list.Add(new DiningCardConsumeRecords()
                {
                    consumeTime = r.消费时间.ToString("yyyy-MM-dd HH:mm"),
                    money = ((decimal)r.消费金额).ToString("0.0"),
                    diningType = r.餐别,
                    place = r.消费场所
                });
            }
            WriteEventLog("饭卡查询", "消费记录：" + from_date + "~" + to_date + ":查到的条数：" + list.Count());
            return Json(new { suc = true, records = list });
        }

        //充值记录
        [SessionTimeOutJsonFilter]
        public JsonResult GetRechargeRecords(string from_date, string to_date)
        {
            var records = canteenDb.ljq20160323_003(userInfo.cardNo, from_date, to_date).ToList();
            if (records.Count() < 1)
            {
                WriteEventLog("饭卡查询", "充值记录：" + from_date + "~" + to_date + ":此时间段查询不到相关记录");
                return Json(new SimpleResultModel() { suc = false, msg = "此时间段查询不到相关记录" });
            }
            List<DiningCardRechargeRecords> list = new List<DiningCardRechargeRecords>();
            foreach (var r in records.OrderByDescending(r=>r.充值时间))
            {
                list.Add(new DiningCardRechargeRecords()
                {
                    beforeSum = ((decimal)r.充值前金额).ToString("0.0"),
                    afterSum = ((decimal)r.充值后金额).ToString("0.0"),
                    rechargeSum = ((decimal)r.充值金额).ToString("0.0"),
                    rechargeTime = r.充值时间.ToString("yyyy-MM-dd HH:mm"),
                    place = r.充值场所
                });
            }
            WriteEventLog("饭卡查询", "充值记录：" + from_date + "~" + to_date + ":查到的条数：" + list.Count());
            return Json(new { suc = true, records = list });
        }

        //挂失和解除挂失
        [SessionTimeOutJsonFilter]
        public JsonResult LockOrUnlock(string lockStatus)
        {
            if (lockStatus.Equals("1") || lockStatus.Equals("2")) {
                try {
                    canteenDb.ljq20161019(userInfo.cardNo, lockStatus);
                }
                catch (Exception ex) {
                    return Json(new SimpleResultModel() { suc = false, msg = "操作失败,原因：" + ex.InnerException.Message });
                }
                CleardinningCarStatus();  //刷新饭卡状态

                WriteEventLog("饭卡挂失", "成功" + (lockStatus.Equals("1") ? "解除" : "申请") + "挂失");
                return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });

            }
            return Json(new SimpleResultModel() { suc = false, msg = "操作失败" });
        }

                
        //饭卡绑定
        [SessionTimeOutJsonFilter]
        public JsonResult GetDinnerCardBinding()
        {
            var bindings = canteenDb.t_UserConfig.Where(t => t.FNumber == userInfo.cardNo);
            if (bindings.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "没有绑卡信息" });
            }
            var binding = bindings.First();
            return Json(new { suc = true, status = binding.FStatus, limit = binding.FQuota });
        }


        [SessionTimeOutJsonFilter]
        public JsonResult SaveDinnerCardBinding(string canConsume, string payPassword, int maxLimit)
        {
            if (maxLimit < 0 || maxLimit > 100) {
                return Json(new SimpleResultModel() { suc = false, msg = "免密限额必须介于0与100之间" });
            }
            string phone = userInfoDetail.phone;
            if (string.IsNullOrEmpty(phone)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存设定需要先绑定手机长号，请在主界面点击头像设置手机长号" });
            }
            else {
                var phoneExists = db.ei_users.Where(u => u.phone == phone && u.name != userInfo.name).Select(u=>u.name).Distinct().ToArray();
                if (phoneExists.Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "你的手机长号与[" + string.Join(",", phoneExists) + "]重复，保存失败。" });
                }
            }
            var passwordRegx = new Regex(@"^\d{6}$");
            var hasBindingRecord = canteenDb.t_UserConfig.Where(t => t.FNumber == userInfo.cardNo);
            if (hasBindingRecord.Count() > 0) {
                var record = hasBindingRecord.First();
                if (!"之前设定密码".Equals(payPassword)) {
                    if (!passwordRegx.IsMatch(payPassword)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "支付密码必须为6位数字" });
                    }
                    else {
                        record.FPassword = MyUtils.getNormalMD5(payPassword);
                    }
                }
                record.FPhone = phone;
                record.FStatus = canConsume.Equals("1") ? "1" : "0";
                record.FQuota = maxLimit;
            }
            else {
                if (!passwordRegx.IsMatch(payPassword)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "支付密码必须为6位数字" });
                }
                t_UserConfig binding = new t_UserConfig();
                binding.FNumber = userInfo.cardNo;
                binding.FPassword = MyUtils.getNormalMD5(payPassword);
                binding.FPhone = userInfoDetail.phone;
                binding.FStatus = canConsume.Equals("1") ? "1" : "0";
                binding.FQuota = maxLimit;

                canteenDb.t_UserConfig.Add(binding);
            }

            try {
                canteenDb.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "服务器错误，保存失败，请联系管理员" });
            }

            return Json(new SimpleResultModel() { suc = true, msg = "保存设定成功！" }); 
        }

        #endregion

        #region 检查信息是否健全

        //手机信息
        [SessionTimeOutJsonFilter]
        public JsonResult ValidatePhone()
        {
            if (string.IsNullOrEmpty(userInfoDetail.phone) && string.IsNullOrEmpty(userInfoDetail.shortPhone)) {
                return Json(new SimpleResultModel() { suc = false, msg = "手机号码未填写，请先点击头像完善个人信息" });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        //邮箱信息
        public JsonResult ValidateEmail()
        {
            if (string.IsNullOrEmpty(userInfoDetail.email)) {
                return Json(new SimpleResultModel() { suc = false, msg = "检测到你的邮箱地址未填写，请先点击头像完善个人信息，邮箱地址不限于信利邮箱，可以是qq、网易邮箱或其他邮箱。" });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        #endregion

        #region 工资查询

        [SessionTimeOutFilter]
        public ActionResult CheckSalary()
        {
            //2018-10-11起，不再提供查询工资服务
            ViewBag.tip = "接上级通知，不再提供工资查询服务";
            return View("Error");

            //string salaryNo = userInfoDetail.salaryNo;
            //if(string.IsNullOrEmpty(salaryNo))
            //{
            //    ViewBag.tip = "你的工资账号不存在";
            //    return View("Error");
            //}

            //ViewData["salaryNo"] = salaryNo;
            //var info = db.GetSalaryInfo_new(salaryNo).ToList();
            //ViewData["months"] = db.GetSalaryMonths(salaryNo).ToList();
            //ViewData["info"] = info;
            //WriteEventLog("工资查询", "进入工资查询页面");
            //return View();
        }

        public JsonResult CheckSalarySummary(string yearMonth)
        {            
            DateTime firstDay = DateTime.Parse(yearMonth + "-01");
            DateTime lastDay = firstDay.AddMonths(1);
            yearMonth = yearMonth.Replace("-", "");

            WriteEventLog("工资查询", "查询工资月度摘要:"+yearMonth);
            return Json(db.GetSalarySummary(userInfoDetail.salaryNo, firstDay, lastDay,yearMonth).ToList().First());
        }

        public JsonResult CheckSalaryDetail(string yearMonth)
        {
            DateTime firstDay = DateTime.Parse(yearMonth + "-01");
            DateTime lastDay = firstDay.AddMonths(1);
            yearMonth = yearMonth.Replace("-", "");

            WriteEventLog("工资查询", "查询工资月度明细:" + yearMonth);
            var result = db.GetSalaryAllDetail(userInfoDetail.salaryNo, firstDay, lastDay,yearMonth).ToList();
            if (result.Count() == 0) {
                return Json(new { suc = false, msg = "查询不到此月份的工资数据" });
            }
            return Json(new { suc = true, result = result });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckMonthSalary(string param)
        {
            int salaryNo = int.Parse(userInfoDetail.salaryNo);
            DateTime dt;
            if (!DateTime.TryParse(param, out dt)) {
                ViewBag.tip = "参数出错";
                return View("Error");
            }
            ViewData["yearMonth"] = dt.ToString("yyyy-MM");

            return View();
        }

        #endregion

        #region 公积金信息维护

        public ActionResult EditPublicFundInfo()
        {
            ViewData["info"] = db.ei_public_fund.Where(e => e.证件号码 == userInfoDetail.idNumber).FirstOrDefault();
            ViewData["items"] = db.public_fund_item.ToList();
            return View();
        }

        public JsonResult SavePublicFundInfo(PublicFundModel m)
        {
            var info = db.ei_public_fund.Where(e => e.证件号码 == userInfoDetail.idNumber).FirstOrDefault();
            info.手机号码 = m.telephone;
            info.固定电话号码 = m.phone;
            info.婚姻状况 = m.mariageStatus;
            info.家庭月收入 = m.familyIncome;
            info.家庭住址 = m.familyAddr;
            info.性别 = m.empSex;
            info.姓名全拼 = m.empNamePY;
            info.学历 = m.educationLevel;
            info.邮政编码 = m.postcode;
            info.职称 = m.jobTitle;
            info.职务 = m.jobContent;
            info.职业 = m.job;

            try {
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }

            WriteEventLog("个人公积金信息", "保存成功");
            return Json(new { suc = true, msg = "保存成功！" });
        }

        #endregion


        public ActionResult test()
        {
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult DormGroupIndex()
        {
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;
            ViewData["aesOpenId"] = userInfoDetail.AesOpenId;
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult WorkGroupIndex()
        {
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;            

            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult EleProcess()
        {            
            return View();
        }

        //public string img()
        //{
        //    foreach (var user in db.ei_users.Where(u => u.id>4000 && u.portrait != null)) {
        //        user.short_portrait = MyUtils.MakeThumbnail(MyUtils.BytesToImage(user.portrait));
        //    }
        //    db.SaveChanges();
        //    return "suc";
        //}
        
    }
}
