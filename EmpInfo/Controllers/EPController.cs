using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;

namespace EmpInfo.Controllers
{
    public class EPController : BaseController
    {

        public ActionResult EquitmentDeps()
        {
            return View();
        }

        public JsonResult GetEQDeps(string searchContent = "")
        {
            var rows = (from e in db.ei_epEqDeps
                        join pd in db.ei_epPrDeps on e.id equals pd.eq_dep_id into tp
                        from p in tp.DefaultIfEmpty()
                        where e.dep_num.Contains(searchContent)
                        || e.dep_name.Contains(searchContent)
                        || p.dep_num.Contains(searchContent)
                        || p.dep_name.Contains(searchContent)
                        select new
                        {
                            id = e.id,
                            depNum = e.dep_num,
                            depName = e.dep_name,
                            charger = e.charger_name,
                            minister = e.minister_name,
                            isForbit = e.is_forbit
                        }).Distinct().OrderBy(e => e.depName).ToList();
            if (rows.Count() == 0) {
                return Json(new { suc = false, msg = "查询不到符合条件的设备部门" });
            }
            return Json(new { suc = true, rows = rows });
        }

        public JsonResult AddEqDep(string depNum, string depName, string charger, string minister)
        {
            if (string.IsNullOrEmpty(depNum)) {
                return Json(new { suc = false, msg = "设备部门编码不能为空" });
            }
            if (string.IsNullOrEmpty(depName)) {
                return Json(new { suc = false, msg = "设备部门名称不能为空" });
            }
            if (string.IsNullOrEmpty(charger)) {
                return Json(new { suc = false, msg = "设备部门负责人不能为空" });
            }
            if (string.IsNullOrEmpty(minister)) {
                return Json(new { suc = false, msg = "设备部门分部长不能为空" });
            }
            if (db.ei_epEqDeps.Where(e => e.dep_num == depNum).Count() > 0) {
                return Json(new { suc = false, msg = "设备部门编码已存在，保存失败" });
            }
            if (db.ei_epEqDeps.Where(e => e.dep_name == depName).Count() > 0) {
                return Json(new { suc = false, msg = "设备部门名称已存在，保存失败" });
            }
            var dep = new ei_epEqDeps();
            dep.dep_num = depNum;
            dep.dep_name = depName;
            try {
                if (!string.IsNullOrEmpty(charger)) {
                    dep.charger_name = charger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.charger_num = charger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
                if (!string.IsNullOrEmpty(minister)) {
                    dep.minister_name = minister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.minister_num = minister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = "负责人或分部长出现错误："+ex.Message });
            }

            dep.create_date = DateTime.Now;
            dep.creater_name = userInfo.name;
            dep.creater_num = userInfo.cardNo;

            try {
                db.ei_epEqDeps.Add(dep);
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = "保存出现错误：" + ex.Message });
                throw;
            }


            return Json(new { suc = true, msg = "保存成功" });
        }

        public ActionResult EqDepDetail(int id)
        {
            ViewData["eqDep"] = db.ei_epEqDeps.Single(e => e.id == id);
            ViewData["prDepList"] = db.ei_epPrDeps.Where(e => e.eq_dep_id == id).ToList();
            ViewData["userList"] = (from e in db.ei_epEqUsers
                                    join u in db.ei_users on e.user_id equals u.id
                                    where e.eq_dep_id == id
                                    select new PrDepUserModel()
                                    {
                                        id = u.id,
                                        name = u.name,
                                        number = u.card_number,
                                        jobPosition = e.job_position
                                    }).ToList();
            return View();
        }

        public JsonResult UpdateEqDep(int id, string depNum, string depName, string charger, string minister, int isForbit)
        {
            if (string.IsNullOrEmpty(depNum)) {
                return Json(new { suc = false, msg = "设备部门编码不能为空" });
            }
            if (string.IsNullOrEmpty(depName)) {
                return Json(new { suc = false, msg = "设备部门名称不能为空" });
            }
            if (string.IsNullOrEmpty(charger)) {
                return Json(new { suc = false, msg = "设备部门负责人不能为空" });
            }
            if (string.IsNullOrEmpty(minister)) {
                return Json(new { suc = false, msg = "设备部门分部长不能为空" });
            }
            if (db.ei_epEqDeps.Where(e => e.dep_num == depNum && e.id != id).Count() > 0) {
                return Json(new { suc = false, msg = "设备部门编码已存在，保存失败" });
            }
            if (db.ei_epEqDeps.Where(e => e.dep_name == depName && e.id != id).Count() > 0) {
                return Json(new { suc = false, msg = "设备部门名称已存在，保存失败" });
            }
            var dep = db.ei_epEqDeps.Single(d => d.id == id);
            dep.dep_num = depNum;
            dep.dep_name = depName;
            dep.is_forbit = isForbit == 1 ? true : false;
            try {
                if (!string.IsNullOrEmpty(charger)) {
                    dep.charger_name = charger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.charger_num = charger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
                if (!string.IsNullOrEmpty(minister)) {
                    dep.minister_name = minister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.minister_num = minister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = "负责人或分部长出现错误：" + ex.Message });
            }            

            try {
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = "保存出现错误：" + ex.Message });
                throw;
            }


            return Json(new { suc = true, msg = "保存成功" });
        }

        public JsonResult RemoveEqDep(int id)
        {
            //todo:如果已申请的流程有这个设备部门，那么不能删除
            try {
                var dep = db.ei_epEqDeps.Single(e => e.id == id);
                foreach (var u in dep.ei_epEqUsers.ToList()) {
                    db.ei_epEqUsers.Remove(u);
                }
                foreach (var p in dep.ei_epPrDeps.ToList()) {
                    if (db.ei_epApply.Where(e => e.produce_dep_name == p.dep_name).Count() > 0) {
                        return Json(new SimpleResultModel() { suc = false, msg = "此设备部门关联的生产部门【"+p.dep_name+"】已有报障申请记录，不可删除" });
                    }
                    db.ei_epPrDeps.Remove(p);
                }
                db.ei_epEqDeps.Remove(dep);
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }

            return Json(new { suc = true });
        }

        public JsonResult ToggleForbitStatus(int id)
        {
            try {
                var dep = db.ei_epEqDeps.Single(e => e.id == id);
                dep.is_forbit = !dep.is_forbit;
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }

            return Json(new { suc = true });
        }

        public JsonResult AddEqUsers(int depId, string role, string userInfo)
        {
            try {
                List<PrDepUserModel> userList = new List<PrDepUserModel>();
                var users = userInfo.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var u in users) {
                    var cardNumber = GetUserCardByNameAndCardNum(u);
                    var user = db.ei_users.Single(us => us.card_number == cardNumber);
                    if (db.ei_epEqUsers.Where(e => e.eq_dep_id == depId && e.job_position == role && e.user_id == user.id).Count() > 0) {
                        continue;
                    }
                    ei_epEqUsers eUser = new ei_epEqUsers();
                    eUser.eq_dep_id = depId;
                    eUser.job_position = role;
                    eUser.ei_users = user;
                    db.ei_epEqUsers.Add(eUser);

                    userList.Add(new PrDepUserModel() { name = user.name, number = user.card_number, jobPosition = role, id = user.id });
                }

                db.SaveChanges();
                return Json(new { suc = true, list = userList });
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }            

        }

        public JsonResult RemoveUserInDep(int depId, string userIds)
        {
            try {
                var userIdInt = userIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(u => Int32.Parse(u)).ToList();
                var depUsers = db.ei_epEqUsers.Where(e => e.eq_dep_id == depId && userIdInt.Contains((int)e.user_id));
                foreach (var depUser in depUsers) {
                    db.ei_epEqUsers.Remove(depUser);
                }
                db.SaveChanges();
                return Json(new { suc = true,msg="成功移出选择的人员" });
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }
        }

        public JsonResult SavePrDeps(int prId, int depId, string prDepNo, string prDepName, string busDepName, string prDepCharger, string prDepChief, string prDepMinister)
        {
            if (string.IsNullOrEmpty(prDepNo)) {
                return Json(new { suc = false, msg = "生产部门编码不能为空" });
            }
            if (string.IsNullOrEmpty(prDepName)) {
                return Json(new { suc = false, msg = "生产部门名称不能为空" });
            }
            if (string.IsNullOrEmpty(busDepName)) {
                return Json(new { suc = false, msg = "所属事业部名称不能为空" });
            }
            if (string.IsNullOrEmpty(prDepCharger)) {
                return Json(new { suc = false, msg = "生产部门主管不能为空" });
            }

            ei_epPrDeps dep;
            if (prId == 0) {
                if (db.ei_epPrDeps.Where(e => e.dep_num == prDepNo).Count() > 0) {
                    return Json(new { suc = false, msg = "此生产部门编码已存在，不能重复保存" });
                }
                if (db.ei_epPrDeps.Where(e => e.dep_name == prDepName).Count() > 0) {
                    return Json(new { suc = false, msg = "此生产部门名称已存在，不能重复保存" });
                }
                dep = new ei_epPrDeps();
                dep.create_date = DateTime.Now;
                dep.creater_name = userInfo.name;
                dep.creater_num = userInfo.cardNo;
                db.ei_epPrDeps.Add(dep);
            }
            else {
                dep = db.ei_epPrDeps.Single(d => d.id == prId);
            }

            try {                
                dep.eq_dep_id = depId;
                dep.dep_num = prDepNo;
                dep.dep_name = prDepName;
                dep.bus_dep_name = busDepName;
                dep.charger_name = prDepCharger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                dep.charger_num = prDepCharger.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                if (!string.IsNullOrEmpty(prDepChief)) {
                    dep.chief_name = prDepChief.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.chief_num = prDepChief.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
                if (!string.IsNullOrEmpty(prDepMinister)) {
                    dep.minister_name = prDepMinister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    dep.minister_num = prDepMinister.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)[1];
                }
                db.SaveChanges();
                return Json(new { suc = true, msg = "保存生产部门成功", prDepId = dep.id });
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }

        }

        public JsonResult GetPrDep(int id)
        {
            var dep = db.ei_epPrDeps.Where(p => p.id == id).Select(p => new
            {
                p.id,
                p.dep_name,
                p.dep_num,
                p.bus_dep_name,
                p.charger_num,
                p.charger_name,
                p.chief_name,
                p.chief_num,
                p.minister_name,
                p.minister_num
            }).FirstOrDefault();
            if (dep == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "此生产部门不存在" });
            }
            return Json(new { suc = true, dep = dep });
        }

        public JsonResult GetRepairInfoByPropertyNumber(string propertyNum)
        {
            var info = db.ei_epApply.Where(e => e.property_number == propertyNum).OrderByDescending(e => e.id).FirstOrDefault();
            if (info == null) {
                return Json(new SimpleResultModel() { suc = false });
            }
            return Json(new { suc = true, info = new {info.produce_dep_addr, info.equitment_name,info.equitment_supplier,info.equitment_modual } });
        }

        public JsonResult RemovePrDeps(string prDepIds)
        {
            try {
                var prDepIdInt = prDepIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(u => Int32.Parse(u)).ToList();
                var prDeps = db.ei_epPrDeps.Where(e => prDepIdInt.Contains(e.id));
                foreach (var prDep in prDeps) {
                    if (db.ei_epApply.Where(e => e.produce_dep_name == prDep.dep_name).Count() > 0) {
                        return Json(new SimpleResultModel() { suc = false, msg = "生产部门【" + prDep.dep_name + "】已有报障申请记录，不可删除" });
                    }
                    db.ei_epPrDeps.Remove(prDep);
                }
                db.SaveChanges();
                return Json(new { suc = true, msg = "成功移除所选生产部门" });
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message });
            }
        }

    }
}
