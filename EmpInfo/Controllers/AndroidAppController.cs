using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.EmpWebSvr;

namespace EmpInfo.Controllers
{
    public class AndroidAppController : BaseController
    {

        public ActionResult PushNotice()
        {
            return View();
        }

        public ActionResult PushClickView(int id, string card_no = "")
        {
            ei_pushMsg msg = db.ei_pushMsg.Single(p => p.id == id);

            //保存用户点击事件
            if (!string.IsNullOrEmpty(card_no)) {
                ei_PushResponse res;
                if (msg.ei_PushResponse.Where(r => r.card_no == card_no).Count() > 0) {
                    res = msg.ei_PushResponse.Where(r => r.card_no == card_no).First();
                    res.click_date = DateTime.Now;
                }
                else {
                    res = new ei_PushResponse();
                    res.ei_pushMsg = msg;
                    res.card_no = card_no;
                    res.click_date = DateTime.Now;
                    db.ei_PushResponse.Add(res);
                }
                db.SaveChanges();
            }
            ViewData["title"] = msg.send_title;
            ViewData["content"] = msg.send_content;

            return View();
        }

    }
}
