using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.Filter;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    public class BIController : BaseController
    {
        public ActionResult LinkManagement()
        {
            return View();
        }

        public JsonResult GetMyLinks()
        {
            var result = db.bi_links.Where(b => b.user_no == userInfo.cardNo).ToList();
            return Json(result);
        }

        public JsonResult SaveLink(string obj)
        {
            bi_links link = JsonConvert.DeserializeObject<bi_links>(obj);

            link.update_time = DateTime.Now;
            link.login_password = MyUtils.AESEncrypt(link.login_password);
            try {
                if (link.id > 0) {
                    var existedLink = db.bi_links.Where(b => b.id == link.id).SingleOrDefault();
                    MyUtils.CopyPropertyValue(link, existedLink);
                }
                else {
                    link.user_no = userInfo.cardNo;
                    link.user_name = userInfo.name;
                    db.bi_links.Add(link);
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel());
        }

        public JsonResult RemoveLink(int id)
        {
            try {
                bi_links link = db.bi_links.Where(b => b.id == id).FirstOrDefault();
                if (link != null) {
                    db.bi_links.Remove(link);
                    db.SaveChanges();
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel());
        }

    }
}
