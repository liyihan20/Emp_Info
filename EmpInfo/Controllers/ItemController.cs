using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.Filter;
using EmpInfo.Services;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    
    public class ItemController : BaseController
    {
        [SessionTimeOutJsonFilter]
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

        [SessionTimeOutJsonFilter]
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

        //在职（有部门）的员工查询
        [SessionTimeOutJsonFilter]
        public JsonResult SearchWorkingEmp(string queryString)
        {
            var result = (from u in db.vw_ei_simple_users
                          where (u.card_number.Contains(queryString)
                          || u.name.Contains(queryString))
                          && (u.short_dep_name != null || u.card_number.StartsWith("GN"))
                          select new
                          {
                              cardNumber = u.card_number,
                              userName = u.name,
                              depName = u.short_dep_name
                          }).Take(100).ToList();
            return Json(new { suc = true, result = result });
        }

        //跳转到图片
        public ActionResult ShowPic(string picName)
        {
            ViewData["picName"] = picName;
            return View();
        }

        //选择部门,用于用户选择
        public JsonResult GetDepartmentTreeForSel()
        {
            var list = new List<Department>();
            var deps = db.ei_department.Where(d => (d.FIsDeleted == null || d.FIsDeleted == false) && (d.FIsForbit == null || d.FIsForbit == false)).ToList();
            foreach (var d in db.ei_department.Where(d => d.FNumber.Length == 1 && (d.FIsDeleted == null || d.FIsDeleted == false) && (d.FIsForbit == null || d.FIsForbit == false)).ToList()) {
                list.Add(GetDepartment(deps,d.FNumber));
            }
            return Json(list);
        }

        private Department GetDepartment(List<ei_department> deps, string rootNumber)
        {
            var rootDep = deps.Single(e => e.FNumber == rootNumber);            
            Department dep = new Department();            
            dep.text = rootDep.FName;
            dep.tags = new string[] { rootDep.FNumber,rootDep.id.ToString() };
            dep.selectable = true;
            if (rootDep.FIsForbit == true) {
                dep.color = "#d9534f"; //被禁用显示红色
            }

            dep.nodes = new List<Department>();
            foreach (var child in deps
                .Where(e => e.FParent == rootNumber)
                .OrderBy(e => e.FNumber).ToList()) {
                dep.nodes.Add(GetDepartment(deps,child.FNumber)); //递归获取子节点
            }
            if (dep.nodes.Count() == 0) {
                dep.nodes = null; //没有子节点
            }
            return dep;
        }

        //选择部门，用于部门管理员选择后做报表，只能看到自己的
        public JsonResult GetAdminDepartmentTreeForSel()
        {
            var list = new List<Department>();
            var deps = db.ei_department.Where(d => d.FIsDeleted == null || d.FIsDeleted == false).ToList();
            foreach (var d in db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo) && (d.FIsDeleted == false || d.FIsDeleted == null)).Distinct().ToList()) {
                list.Add(GetDepartment(deps, d.FNumber));
            }
            return Json(list);
        }

        //人事系统的部门树
        public JsonResult GetHRDepartmentTree()
        {
            var list = new List<Department>();
            var deps = db.vw_hr_department.ToList();
            foreach (var d in db.vw_hr_department.Where(h=>h.id==1661).ToList()) {
                list.Add(GetHRDepartment(deps, d.id));
            }
            
            return Json(list);
        }
        //获取人事系统的部门
        private Department GetHRDepartment(List<vw_hr_department> deps, int rootId)
        {
            var rootDep = deps.Single(e => e.id == rootId);
            Department dep = new Department();
            dep.text = rootDep.short_name;
            dep.tags = new string[] { rootDep.id.ToString(), rootDep.id.ToString() };
            dep.selectable = true;            

            dep.nodes = new List<Department>();
            foreach (var child in deps
                .Where(e => e.parent_id == rootId)
                .OrderBy(e => e.id).ToList()) {
                dep.nodes.Add(GetHRDepartment(deps, child.id)); //递归获取子节点
            }
            if (dep.nodes.Count() == 0) {
                dep.nodes = null; //没有子节点
            }
            return dep;
        }


        //获取k3的客户名称，通过客户编码
        public JsonResult GetK3CustomerName(string customerNumber, string company)
        {
            try {
                var list = db.GetK3CustomerNameByNum(customerNumber, company).ToList();
                if (list.Count() < 1) {
                    return Json(new SimpleResultModel() { suc = false, msg = "客户不存在，请确认客户编码是否正确" });
                }
                else {
                    return Json(new SimpleResultModel() { suc = true, extra = list.First() });
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        /// <summary>
        /// 图片附件列表
        /// </summary>
        /// <param name="sysNo"></param>
        /// <returns></returns>
        public ActionResult PicAttachments(string sysNo)
        {
            ViewData["atts"] = MyUtils.GetAttachmentInfo(sysNo);
            ViewData["sysNo"] = sysNo;
            return View();
        }

        /// <summary>
        /// 获取附件信息
        /// </summary>
        /// <param name="sysNo"></param>
        /// <returns></returns>
        public JsonResult GetAttachments(string sysNo)
        {
            var result = MyUtils.GetAttachmentInfo(sysNo);
            return Json(result);
        }

        /// <summary>
        /// 获取紧急出货运输申请中客户对应的地址
        /// </summary>
        /// <param name="customerNumber"></param>
        /// <returns></returns>
        public JsonResult GetETCustomerAddr(string customerNumber, string company)
        {
            return Json(new ETSv().getCustomerAddr(customerNumber, company));
        }

        /// <summary>
        /// 通过K3代码获取名称、型号和单位
        /// </summary>
        /// <param name="account"></param>
        /// <param name="itemNo"></param>
        /// <returns></returns>
        public JsonResult GetK3ItemByNo(string account, string itemNo)
        {
            var result = db.GetK3Item(account, itemNo).ToList();
            if (result.Count() > 0) {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result.First()) });
            }
            else {
                return Json(new SimpleResultModel() { suc = false });
            }
        }

        /// <summary>
        /// 辅料订购流程中的事业部和申购部门
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public JsonResult GetApBusAndDepNameInK3(string account)
        {
            var result = db.GetApBusAndDepNamesInK3(account).ToList();
            if (result.Count() > 0) {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            else {
                return Json(new SimpleResultModel() { suc = false });
            }
        }

        /// <summary>
        /// 辅料订购流程的审核人员都可以修改订料数量
        /// </summary>
        /// <param name="sysNo"></param>
        /// <param name="entryNo"></param>
        /// <param name="qty"></param>
        /// <returns></returns>
        public JsonResult UpdateApQty(string sysNo, int entryNo, decimal qty)
        {
            try {
                var ap = new APSv(sysNo);
                ap.UpdateQty(entryNo, qty,userInfo.name);
                return Json(new SimpleResultModel() { suc = true });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：" + ex.Message });
            }
        }


        public JsonResult GetAPPriceHistory(string itemNo)
        {
            try {
                var result = new APSv().GetItemPriceHistory(itemNo);
                if (result != null) {
                    return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
                }
                else {
                    return Json(new SimpleResultModel() { suc = false, msg = "找不到相关的采购订单历史记录" });
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetAPQtyHistory(string itemNumber, string busName, string beginDate, string endDate)
        {
            DateTime bd, ed;
            if (!DateTime.TryParse(beginDate, out bd)) {
                return Json(new SimpleResultModel() { suc = false, msg = "开始日期不合法" });
            }
            if (!DateTime.TryParse(endDate, out ed)) {
                return Json(new SimpleResultModel() { suc = false, msg = "结束日期不合法" });
            }
            try {
                var result = new APSv().GetItemQtyHistory(itemNumber, busName, bd, ed);
                if (result.Count() > 0) {
                    return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
                }
                else {
                    return Json(new SimpleResultModel() { suc = false, msg = "当前查询条件找不到相关的采购订单历史记录" });
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetPOInfoFromK3(string account, string poNumber)
        {
            try {
                var result = new APSv().GetPOInfoFromK3(account, poNumber);
                if (result.Count() < 1) {
                    return Json(new SimpleResultModel() { suc = false, msg = "找不到符合条件的采购订单" });
                }
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetItemStockQtyFromK3(string itemNumber)
        {
            try {
                var result = new APSv().GetItemStockQtyFromK3(itemNumber);
                if (result.Count() < 1) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此物料当前在各事业部不存在库存" });
                }
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetHREmpInfoDetail(string cardNumber,string empStatus = "")
        {
            GetHREmpInfoDetail_Result result;
            try {
                result = new HRDBSv().GetHREmpDetailInfo(cardNumber);
            }
            catch {
                return Json(new SimpleResultModel(false, "连接人事系统数据库失败，请稍后再试"));
            }
                   
            if (result == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "获取不到此厂牌的人事系统信息" });
            }
            if (!string.IsNullOrWhiteSpace(empStatus)) {
                if (result.emp_status != empStatus) {
                    return Json(new SimpleResultModel() { suc = false, msg = "操作失败，此厂牌的当前状态是:" + result.emp_status });
                }
            }
            return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            
        }

        public JsonResult GetSPExInfo(string sysNum)
        {
            try {
                var result = new SPSv(sysNum).GetExInfo();
                if (result.Count() == 0) return Json(new SimpleResultModel() { suc = false, msg = "查询不到任何快递信息" });
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetSPExInfoWithoutBill(string spJson)
        {
            try {
                //WriteEventLog("快递", spJson);
                ei_spApply sp = JsonConvert.DeserializeObject<ei_spApply>(spJson);
                var result = new SPSv().GetExInfoWithoutBill(sp);
                if (result.Count() == 0) return Json(new SimpleResultModel() { suc = false, msg = "查询不到任何快递信息" });
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        public JsonResult GetEmpNameByNumber(string cardNumber)
        {
            var empName = db.ei_users.Where(u => u.card_number == cardNumber).Select(u=>u.name).FirstOrDefault();
            if (empName == null) {
                return Json(new SimpleResultModel(false, "此厂牌不存在或未在此系统注册"));
            }
            return Json(new SimpleResultModel(true, "", empName));
        }
                

        public string test()
        {
            //foreach (var l in db.k3_database.Where(d=>!d.account_name.Contains("总部")).ToList()) {
            //    var result = db.Database.SqlQuery<string>(string.Format("select 1 from [{0}].{1}.dbo.poorderentry where fdate is null", l.server_ip, l.database_name)).ToList();
            //    if(result.Count()<1){
            //        return l.account_name;
            //    }
            //}

            return "OK";
        }

    }
}
