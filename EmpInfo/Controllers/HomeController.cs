using EmpInfo.Filter;
using EmpInfo.Models;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
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

            return View();
        }

        //获取头像
        public ActionResult GetEmpPortrait(string card_no)
        {
            if (string.IsNullOrEmpty(card_no)) card_no = userInfo.cardNo;
            var user = db.ei_users.Single(u => u.card_number == card_no);
            byte[] portrait = user.short_portrait;
            if (portrait == null) {
                string picUrl = Server.MapPath("~/Content/images/") + (user.sex.Equals("男") ? "user_man.png" : "user_woman.png");
                portrait = MyUtils.GetServerImage(picUrl);
            }
            return File(portrait, @"image/bmp");
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
            return Json(new { suc = true, phone = userInfoDetail.phone, email = userInfoDetail.email, shortPhone = userInfoDetail.shortPhone });
        }

        //更新个人信息
        [SessionTimeOutJsonFilter]
        public JsonResult UpdatePersonalInfo(string phone, string email, string new_pass,string shortPhone)
        {
            var user = db.ei_users.Single(u => u.card_number == userInfo.cardNo);
            if (!string.IsNullOrEmpty(email))
            {
                var emailR = new Regex(@"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$");
                if (!emailR.IsMatch(email))
                {
                    return Json(new SimpleResultModel() { suc = false, msg = "邮箱地址不合法" });
                }
                if (db.ei_users.Where(u => u.email == email && u.id != userInfo.id).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此邮箱已被其他人绑定" });
                }
                user.email = email;
            }
            if (!string.IsNullOrEmpty(phone))
            {
                var phoneR = new Regex(@"^\d{11}$");
                if (!phoneR.IsMatch(phone))
                {
                    return Json(new SimpleResultModel() { suc = false, msg = "手机长号必须是11位数字" });
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

        public ActionResult test()
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
