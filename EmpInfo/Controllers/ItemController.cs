using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.Filter;

namespace EmpInfo.Controllers
{
    [SessionTimeOutJsonFilter]
    public class ItemController : BaseController
    {
        public JsonResult GetUserBySelect()
        {
            var users = (from u in db.ei_users
                         select new SelectModel()
                         {
                             text = u.name + "[" + u.card_number + "]",
                             intValue = u.id
                         }).ToList();
            return Json(users);
        }

        public JsonResult GetAutBySelect()
        {
            var auts = (from a in db.ei_authority
                        select new SelectModel()
                        {
                            text = a.name,
                            intValue = a.id
                        }).ToList();
            return Json(auts);
        }
    }
}
