using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Filter;

namespace EmpInfo.Controllers
{
    [SessionTimeOutFilter]
    public class K3Controller : BaseController
    {
        public ActionResult AccountReset()
        {
            WriteEventLog("K3重开通/重置", "打开处理界面");
            var accountList = (from ac in db.GetK3AccoutList()
                               select new K3AccountModel()
                               {
                                   number = ac.FAcctNumber,
                                   name = ac.FAcctName
                               }).ToList();
            ViewData["k3Accounts"] = accountList;
            ViewData["email"] = userInfoDetail.email;
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult BeginReset(string k3Name, string account, string opType)
        {
            WriteEventLog("K3重开通/重置", string.Format("k3用户名:{0};账套:{1};操作类型:{2}", k3Name, account, opType));
            if (!k3Name.StartsWith(userInfo.name)) {
                WriteEventLog("K3重开通/重置", "你填写的K3登录名与你的真实姓名差别过大，不能处理，请走OA流程");
                return Json(new SimpleResultModel() { suc = false, msg = "你填写的K3登录名与你的真实姓名差别过大，不能处理，请走OA流程" });
            }
            try {
                db.ResetK3Emp(k3Name, userInfo.cardNo, userInfoDetail.phone, account, opType);
            }
            catch (Exception ex) {
                string message = ex.InnerException.Message;
                if (message.Contains("职员信息未完善")) {
                    try {
                        WriteEventLog("K3重开通/重置", "职员信息未完善，尝试完善");
                        db.BindK3Emp(k3Name, userInfo.cardNo, account);
                        db.ResetK3Emp(k3Name, userInfo.cardNo, userInfoDetail.phone, account, opType);
                    }
                    catch (Exception iex) {
                        WriteEventLog("K3重开通/重置", "职员信息未完善，尝试失败：" + iex.InnerException.Message);
                        return Json(new SimpleResultModel() { suc = false, msg = iex.InnerException.Message });
                    }
                }
                else {
                    WriteEventLog("K3重开通/重置", "操作失败："+ex.InnerException.Message);
                    return Json(new SimpleResultModel() { suc = false, msg = ex.InnerException.Message });
                }
            }
            ICAuditEntities db2 = new ICAuditEntities(); //只能new多一个Entity对象，否则会出现在提供程序连接上启动事务时出错的错误，不明白为什么
            ei_k3RestLog k3Log = new ei_k3RestLog();
            k3Log.account = account;
            k3Log.card_number = userInfo.cardNo;
            k3Log.k3_name = k3Name;
            k3Log.op_time = DateTime.Now;
            k3Log.op_type = opType.Equals("1") ? "重新开通" : opType.Equals("2") ? "重置密码" : "重新开通|重置密码";
            db2.ei_k3RestLog.Add(k3Log);
            db2.SaveChanges();

            return Json(new SimpleResultModel() { suc = true });
        }

    }
}
