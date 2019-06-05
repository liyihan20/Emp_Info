using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Services;
using EmpInfo.Models;
using EmpInfo.FlowSvr;

namespace EmpInfo.Controllers
{
    public class TestController : BaseController
    {
        public string UCMsg()
        {
            UCSv sv = new UCSv("UC19050701");
            sv.SendNotification(new FlowResultModel() { suc = true, msg = "完成" });
            return "ok";
        }
                

    }
}
