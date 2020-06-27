using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;

namespace EmpInfo.Controllers
{
    public class PackageController : BaseController
    {
        
        public ActionResult EmpView()
        {
            string areaName = "", dormName = "";
            var dormInfo = db.GetEmpDormInfo(userInfo.cardNo).ToList();
            if (dormInfo.Count() > 0) {
                areaName = dormInfo.First().area;
                dormName = dormInfo.First().dorm_number;
            }
            ViewData["areaName"] = areaName;
            ViewData["dormName"] = dormName;
            return View();
        }

        public JsonResult EmpIsInDorm()
        {
            bool isInDorm = db.GetEmpDormInfo(userInfo.cardNo).Count() > 0;
            return Json(new SimpleResultModel() { suc = isInDorm });
        }

        public JsonResult Register(FormCollection fc)
        {
            string areaName = fc.Get("areaName");
            string dormName = fc.Get("dormName");
            string deliveryCop = fc.Get("deliveryCop");
            string deliveryNo = fc.Get("deliveryNo");
            string deliveryStuff = fc.Get("deliveryStuff");
            DateTime lastWeek = DateTime.Now.AddDays(-7);

            if (string.IsNullOrEmpty(areaName)) {
                return Json(new SimpleResultModel() { suc = false, msg = "没有住宿信息，操作失败！" });
            }
            if (db.ei_deliveryInfo.Where(d => d.deivery_cop == deliveryCop && d.delivery_no == deliveryNo && d.register_date > lastWeek).Count() > 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "此快递编号最近已被登记，操作失败！" });
            }
            try {
                ei_deliveryInfo di = new ei_deliveryInfo();
                di.user_id = userInfo.id;
                di.status = "已登记，配送中";
                di.dorm_area = areaName;
                di.dorm_number = dormName;
                di.delivery_no = deliveryNo;
                di.deivery_cop = deliveryCop;
                di.delivery_stuff = deliveryStuff;
                di.register_date = DateTime.Now;
                di.is_finish = false;
                di.packs = 1;

                db.ei_deliveryInfo.Add(di);
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "提交失败：" + ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "快件服务登记成功!" });

        }

    }
}
