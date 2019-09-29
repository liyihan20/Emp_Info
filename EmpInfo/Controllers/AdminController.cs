using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Filter;
using EmpInfo.Util;
using System.Configuration;
using EmpInfo.Services;
using Newtonsoft.Json;


namespace EmpInfo.Controllers
{
    
    public class AdminController : BaseController
    {
        int pageNumber = 30;
        string[] speciaDepNodeName = new string[] { "AH审批", "行政审批" };
        string[] speciaDepNumber = new string[] { "1","2","101", "106" };
        
        [AuthorityFilter]
        public ActionResult AdminIndex()
        {
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        && a.number > 1 && a.number < 2
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);
            ViewData["autStr"] = autStr;
            return View();
        }

        #region 用户管理        
        [AuthorityFilter]
        public ActionResult UserManagement()
        {
            var userCount = db.ei_users.Count();
            ViewData["userCount"] = userCount;
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetUsers(string searchContent)
        {            
            int total;
            var users = (from v in db.vw_ei_simple_users
                         where v.card_number.Contains(searchContent)
                        || v.name.Contains(searchContent)
                        || v.salary_no.Contains(searchContent)
                        || v.id_number.Contains(searchContent)
                        //|| v.dep_name.Contains(searchContent)
                         select new UserListModel()
                         {
                             name = v.name,
                             sex = v.sex,
                             cardNo = v.card_number,
                             status = v.forbit_flag != true ? "正常" : "禁用",
                             depName = v.short_dep_name
                         }).ToList();
            Session["adminUserList"] = users;
            total = users.Count();
            if (total == 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "没有符合条件的注册员工" });
            }
            return Json(new {suc = true, rows = users.Take(pageNumber), pages = Math.Ceiling((total * 1.0) / pageNumber) });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetUsersPage(int page)
        {
            if(Session["adminUserList"]==null){
                return Json(new { suc = false });
            }
            List<UserListModel> users = (List<UserListModel>)Session["adminUserList"];
            

            return Json(new { suc = true, rows = users.Skip((page - 1) * pageNumber).Take(pageNumber).ToList() });
        }


        [SessionTimeOutFilter]
        public ActionResult CheckUserDetail(string cardNo)
        {
            var users = db.vw_ei_users.Where(u => u.card_number == cardNo).ToList();
            if (users.Count() != 1)
            {
                return View("error");
            }
            ViewData["user"] = users.First();
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult resetPassword(int id)
        {
            try
            {
                var user = db.ei_users.Single(u => u.id == id);
                user.password = MyUtils.getMD5("000000");
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });                
            }
            return Json(new SimpleResultModel() { suc = true, msg = "密码成功重置为6个0" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult activeUser(int id)
        {
            string msg;
            try
            {
                var user = db.ei_users.Single(u => u.id == id);
                if (user.forbit_flag == true)
                {
                    user.forbit_flag = false;
                    user.last_login_date = DateTime.Now;
                    msg = "用户成功激活";
                }
                else
                {
                    user.forbit_flag = true;
                    msg = "用户成功禁用";
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = msg });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult statisticsUsers()
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            DateTime lastMonth = DateTime.Now.AddMonths(-1);

            int allUserCount = db.ei_users.Count();
            int quitUserCount = db.vw_ei_simple_users.Where(v => v.dep_name == null).Count();
            int wxBindUserCount = db.vw_push_users.Count();
            int androidUserCount = db.ei_users_android.Select(a => a.card_number).Distinct().Count();
            int yearActiveUserCount = db.ei_users.Where(u => u.last_login_date >= lastYear).Count();
            int monthActiveUserCount = db.ei_users.Where(u => u.last_login_date >= lastMonth).Count();
            return Json(new
            {
                suc = true,
                allUserCount = allUserCount.ToString("N0"),
                quitUserCount = quitUserCount.ToString("N0"),
                wxBindUserCount = wxBindUserCount.ToString("N0"),
                androidUserCount = androidUserCount.ToString("N0"),
                yearActiveUserCount = yearActiveUserCount.ToString("N0"),
                monthActiveUserCount = monthActiveUserCount.ToString("N0")
            });
        }

        #endregion

        #region 情景变换
        [SessionTimeOutJsonFilter]        
        public JsonResult ChangeUser(string card_no)
        {
            if (db.ei_users.Where(u => u.card_number == card_no).Count() != 1)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "厂牌不存在或者存在多用户" });
            }
            var user = db.ei_users.Single(u => u.card_number == card_no);
            setcookie(user, 1);
            ClearUserInfoDetail();
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }
        #endregion

        #region 权限管理
        
        [AuthorityFilter]
        public ActionResult Authorities(string searchContent)
        {
            if (searchContent == null) searchContent = "";
            ViewData["searchContent"] = searchContent;
            ViewData["list"] = (from a in db.ei_authority
                                where a.name.Contains(searchContent)
                                || a.en_name.Contains(searchContent)
                                || a.controler_name.Contains(searchContent)
                                || a.action_name.Contains(searchContent)
                                orderby a.number
                                select a).ToList();
            WriteEventLog("权限管理", "获取权限列表,search:"+searchContent);
            return View();
        }

        [SessionTimeOutFilter]
        [HttpPost]
        public ActionResult SaveAuthority(FormCollection fc)
        {
            string number = fc.Get("aut_no").Trim();
            string name = fc.Get("aut_name").Trim();
            string nameEn = fc.Get("aut_en_name").Trim();
            string controlerName = fc.Get("aut_controller").Trim();
            string actionName = fc.Get("aut_action").Trim();
            string iconcls = fc.Get("aut_icon").Trim();
            string description = fc.Get("aut_description").Trim();
            string idStr = fc.Get("aut_id").Trim();            

            try
            {
                //新增
                if (string.IsNullOrEmpty(idStr))
                {
                    if (db.ei_authority.Where(a => a.name == name || a.en_name == nameEn).Count() > 0)
                    {
                        WriteEventLog("权限管理", "该权限已存在，新增失败", -10);
                        ViewBag.tip = "权限已存在，新增失败";
                        return View("Error");
                    }
                    db.ei_authority.Add(new ei_authority()
                    {
                        number = decimal.Parse(number),
                        name = name,
                        en_name = nameEn,
                        controler_name = controlerName,
                        action_name = actionName,
                        iconcls = iconcls,
                        description = description
                    });
                    WriteEventLog("权限管理", "新增权限：" + number + ";" + name);
                }
                else
                {
                    //修改
                    int id = Int32.Parse(idStr);
                    var aut = db.ei_authority.Single(a => a.id == id);
                    aut.number = decimal.Parse(number);
                    aut.name = name;
                    aut.en_name = nameEn;
                    aut.controler_name = controlerName;
                    aut.action_name = actionName;
                    aut.iconcls = iconcls;
                    aut.description = description;
                    WriteEventLog("权限管理", "修改权限：" + number + ";" + name);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ViewBag.tip = ex.Message;
                return View("Error");
            }

            return RedirectToAction("Authorities");
        }

        [SessionTimeOutFilter]
        [HttpGet]
        public ActionResult RemoveAuthority(int id)
        {

            if (db.ei_groupAuthority.Where(g => g.authority_id == id).Count() > 0)
            {
                ViewBag.tip = "权限存在于分组中，不能删除";
                return View("Error");
            }

            try
            {
                var aut = db.ei_authority.Single(a => a.id == id);
                db.ei_authority.Remove(aut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("权限管理", "删除失败：" + ex.Message, -10);
                ViewBag.tip = ex.Message;
                return View("Error");
            }

            WriteEventLog("权限管理", "删除权限" + id);

            return RedirectToAction("Authorities");
        }
        #endregion

        #region 分组管理
        
        [AuthorityFilter]
        public ActionResult Groups()
        {
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetGroups(string searchContent)
        {
            if (string.IsNullOrEmpty(searchContent)) searchContent = "";
            var result = (from g in db.ei_groups
                          where g.name.Contains(searchContent)
                          || g.description.Contains(searchContent)
                          select new
                          {
                              id = g.id,
                              name = g.name,
                              description = g.description
                          }).ToList();
            return Json(new { suc = true, rows = result });
        }


        [SessionTimeOutFilter]
        public ActionResult GroupDetail(int id)
        {
            var group = db.ei_groups.Single(g => g.id == id);
            ViewData["groupId"] = group.id;
            ViewData["groupName"] = group.name;
            ViewData["groupDes"] = group.description;
            ViewData["groupUsers"] = group.ei_groupUser
                .Select(u => new GroupUserModel() { id = u.user_id, cardNo = u.ei_users.card_number, userName = u.ei_users.name })
                .ToList();
            ViewData["groupAuts"] = group.ei_groupAuthority
                .Select(a => new GroupAutModel() { id = a.authority_id,autNumber=a.ei_authority.number, autName = a.ei_authority.name, autDes = a.ei_authority.description })
                .ToList();
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddGroup(string name, string des)
        {
            if (db.ei_groups.Where(g => g.name == name).Count() > 0)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "组名已存在" });
            }
            db.ei_groups.Add(new ei_groups() { name = name, description = des });
            db.SaveChanges();
            return Json(new SimpleResultModel() { suc = true, msg = "分组创建成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult UpdateGroup(int id,string name, string des)
        {
            try
            {
                var group = db.ei_groups.Single(g => g.id == id);
                group.name = name;
                group.description = des;
                db.SaveChanges();
                return Json(new SimpleResultModel() { suc = true, msg = "保存成功" });
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败:"+ex.Message });                
            }
        }


        [SessionTimeOutFilter]
        public ActionResult RemoveGroup(int id)
        {
            try
            {
                var group = db.ei_groups.Single(g => g.id == id);
                foreach (var u in group.ei_groupUser)
                {
                    db.ei_groupUser.Remove(u);
                }
                foreach (var a in group.ei_groupAuthority)
                {
                    db.ei_groupAuthority.Remove(a);
                }
                db.ei_groups.Remove(group);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ViewBag.tip = ex.Message;
                return View("Error");
            }
            return RedirectToAction("Groups");
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddUserToGroup(int group_id, int? user_id, string user_name_no)
        {
            if (user_id == null && string.IsNullOrEmpty(user_name_no)) {
                return Json(new { result = new SimpleResultModel() { suc = false, msg = "请先选择用户" } });
            }
            if (user_id == null && !string.IsNullOrEmpty(user_name_no)) {
                string userNumber = GetUserCardByNameAndCardNum(user_name_no);
                user_id = db.vw_ei_users.Single(v => v.card_number == userNumber).id;
            }
            try
            {
                if (db.ei_groupUser.Where(gu => gu.group_id == group_id && gu.user_id == user_id).Count() > 0)
                {
                    return Json(new { result = new SimpleResultModel() { suc = false, msg = "该用户已存在分组中" } });
                }
                var groupUser = new ei_groupUser();
                groupUser.group_id = group_id;
                groupUser.user_id = user_id;
                db.ei_groupUser.Add(groupUser);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { result = new SimpleResultModel() { suc = false, msg = "添加用户失败:" + ex.Message } });
            }
            GroupUserModel groupU = db.ei_users.Where(u => u.id == user_id)
                .Select(u => new GroupUserModel() { id = u.id, cardNo = u.card_number, userName = u.name }).First();

            return Json(new { result = new SimpleResultModel() { suc = true, msg = "添加用户成功" }, groupUser = groupU });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult RemoveUserInGroup(int group_id, string user_ids)
        {
            string[] userIdArr = user_ids.Split(',');
            try
            {
                foreach (var userIdStr in userIdArr) {
                    int userId = Int32.Parse(userIdStr);
                    var groupUser = db.ei_groupUser.Single(gu => gu.group_id == group_id && gu.user_id == userId);
                    db.ei_groupUser.Remove(groupUser);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "用户移除失败：" + ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "用户移除成功,数量："+userIdArr.Count() });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddAutToGroup(int group_id, int aut_id)
        {
            try
            {
                if (db.ei_groupAuthority.Where(ga => ga.group_id == group_id && ga.authority_id == aut_id).Count() > 0)
                {
                    return Json(new { result = new SimpleResultModel() { suc = false, msg = "权限已存在分组中" } });
                }
                var groupAut = new ei_groupAuthority();
                groupAut.group_id = group_id;
                groupAut.authority_id = aut_id;
                db.ei_groupAuthority.Add(groupAut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { result = new SimpleResultModel() { suc = false, msg = "权限添加失败：" + ex.Message } });
            }
            GroupAutModel groupA = db.ei_authority.Where(a => a.id == aut_id)
                .Select(a => new GroupAutModel() { id = a.id, autNumber = a.number, autName = a.name, autDes = a.description }).First();
            return Json(new { result = new SimpleResultModel() { suc = true, msg = "权限添加成功" }, groupAut = groupA });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult RemoveAutInGroup(int group_id, string aut_ids)
        {
            string[] autIdArr = aut_ids.Split(',');
            try
            {
                foreach (string autIdStr in autIdArr)
                {
                    int autId = Int32.Parse(autIdStr);
                    var groupAut = db.ei_groupAuthority.Single(ga => ga.group_id == group_id && ga.authority_id == autId);
                    db.ei_groupAuthority.Remove(groupAut);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new SimpleResultModel() { suc = false, msg = "权限移除失败：" + ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "权限移除成功" });
        }

        #endregion

        #region PO单归属
        
        [AuthorityFilter]
        public ActionResult POAccountInfo()
        {
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetPOInfo(string poNumber)
        {
            var result = db.GetPOAccount(poNumber).First();
            if (string.IsNullOrEmpty(result.account))
            {
                return Json(new SimpleResultModel() { suc = false, msg = "此PO不存在" });
            }
            else
            {
                return Json(new SimpleResultModel() { suc = true, msg = result.account + ":" + ((DateTime)result.poDate).ToString("yyyy-MM-dd") });
            }


        }

        #endregion

        #region 部门管理

        [SessionTimeOutFilter]
        public ActionResult Department()
        {
            return View();
        }

        public JsonResult GetDepartmentTree()
        {
            var list = new List<Department>();
            var deps = db.ei_department.Where(d => d.FIsDeleted == null || d.FIsDeleted == false).ToList();
            foreach (var d in db.ei_department.Where(d => d.FNumber.Length == 1).ToList()) {
                list.Add(GetDepartment(deps,d.FNumber));
            }
            return Json(list);
        }

        private Department GetDepartment(List<ei_department> deps, string rootNumber, bool isAdmin = false)
        {
            var rootDep = deps.Single(e => e.FNumber == rootNumber);
            var theNodeIsAdmin = isAdmin; //是否管理员节点
            Department dep = new Department();
            dep.text = rootDep.FName;
            dep.tags = new string[] { rootDep.FNumber };
            dep.nodes = new List<Department>();
            if (!theNodeIsAdmin) {
                if (rootDep.FAdmin != null && rootDep.FAdmin.Contains(userInfo.cardNo)) {
                    theNodeIsAdmin = true;
                }
            }
            dep.selectable = theNodeIsAdmin;
            if (rootDep.FIsForbit == true) {
                dep.color = "#d9534f"; //被禁用显示红色
            }
            else if (theNodeIsAdmin) {
                dep.color = "#5cb85c"; //有权限管理显示绿色
            }
            foreach (var child in deps
                .Where(e => e.FParent == rootNumber)
                .OrderBy(e => e.FNumber).ToList()) {
                dep.nodes.Add(GetDepartment(deps, child.FNumber, theNodeIsAdmin)); //递归获取子节点
            }
            if (dep.nodes.Count() == 0) {
                dep.nodes = null; //没有子节点
            }
            return dep;
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetDepartmentInfo(string depNum)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            var auditNode = dep.ei_departmentAuditNode.Count() > 0 ? dep.ei_departmentAuditNode.First() : null;
            return Json(new
            {
                status = dep.FIsForbit == true ? "禁用" : "正常",
                admin = GetUserNameAndCardByCardNum(dep.FAdmin),
                reporter=GetUserNameAndCardByCardNum(dep.FReporter),
                creator = GetUserNameAndCardByCardNum(dep.FCreator),
                createTime = ((DateTime)dep.FCreateDate).ToString("yyyy-MM-dd HH:mm"),
                isAuditNode = dep.FIsAuditNode,
                auditNodeName = auditNode == null ? "" : auditNode.FAuditNodeName,
                auditNodeCounterSign = auditNode == null ? false : auditNode.FIsCounterSign
            });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult ChangeDepartmentName(string depNum, string newName)
        {            
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            dep.FName = newName;
            WriteEventLog("部门管理", "修改名称:" + depNum + "," + dep.FName + "->" + newName);

            db.SaveChanges();
            return Json(new SimpleResultModel() { suc = true, msg = "部门名称修改成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult DeleteDepartment(string depNum)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            if (db.ei_department.Where(e => (e.FIsDeleted == null || e.FIsDeleted == false) && e.FNumber!=dep.FNumber && e.FNumber.StartsWith(dep.FNumber)).Count() > 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "存在子部门，不能删除！" });
            }
            dep.FIsDeleted = true;
            db.SaveChanges();

            WriteEventLog("部门管理", "删除部门：" + depNum);
            return Json(new SimpleResultModel() { suc = true, msg = "部门删除成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AddDepartment(string parentNum, string newDepName)
        {
            

            var eldest = db.ei_department.Where(e => e.FParent == parentNum).OrderByDescending(e => e.FNumber).ToList();
            string childIndex = "01";
            if (eldest.Count() > 0) {
                childIndex =  (int.Parse(eldest.First().FNumber.Substring(parentNum.Length))+1).ToString().PadLeft(2,'0');
            }
            var dep = new ei_department();
            dep.FNumber = parentNum + childIndex;
            dep.FName = newDepName;
            dep.FCreateDate = DateTime.Now;
            dep.FCreator = userInfo.cardNo;
            dep.FParent = parentNum;

            db.ei_department.Add(dep);
            db.SaveChanges();
            WriteEventLog("部门管理", "新增部门：" + dep.FNumber + "," + newDepName);
            return Json(new SimpleResultModel() { suc = true, msg = "部门新增成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult ToggleDepartmentForbit(string depNum)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            dep.FIsForbit = dep.FIsForbit == true ? false : true;
            db.SaveChanges();

            string opName = dep.FIsForbit == true ? "禁用" : "启用";
            WriteEventLog("部门管理", opName + "部门：" + depNum);
            return Json(new SimpleResultModel() { suc = true, msg = "部门" + opName + "成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult ChangeDepAdmins(string depNum, string admins)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            dep.FAdmin = GetUserCardByNameAndCardNum(admins);

            //设置审核人有看到组织架构的权限
            foreach (var admin in admins.Split(new char[] { ';' },StringSplitOptions.RemoveEmptyEntries)) {
                var adminNum = GetUserCardByNameAndCardNum(admin);
                var adminUser = db.ei_users.Single(u => u.card_number == adminNum);
                if (db.ei_groupUser.Where(g => g.group_id == 8 && g.user_id == adminUser.id).Count() == 0) {
                    db.ei_groupUser.Add(new ei_groupUser()
                    {
                        group_id = 8, //组织架构的组别id
                        user_id = adminUser.id
                    });
                }
            }

            db.SaveChanges();

            WriteEventLog("部门管理", depNum + "变更管理员：" + admins);
            return Json(new SimpleResultModel() { suc = true, msg = "管理员更新成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult ChangeDepReporters(string depNum, string reporters)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            dep.FReporter = GetUserCardByNameAndCardNum(reporters);

            db.SaveChanges();

            WriteEventLog("部门管理", depNum + "变更统计员：" + reporters);
            return Json(new SimpleResultModel() { suc = true, msg = "统计员更新成功" });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult ToggleAuditNode(string depNum)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            dep.FIsAuditNode = dep.FIsAuditNode == true ? false : true;
            db.SaveChanges();
            WriteEventLog("部门管理", "切换是否审批节点：" + depNum);
            return Json(new SimpleResultModel() { suc = true });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult AlterAuditNodeName(string depNum, string nodeName)
        {
            if (!speciaDepNumber.Contains(depNum) && speciaDepNodeName.Contains(nodeName)) {
                return Json(new SimpleResultModel() { suc = false, msg = "【" + nodeName + "】是系统保留关键字，请修改为其他名称" });
            }
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            var depNodes = dep.ei_departmentAuditNode.ToList();
            ei_departmentAuditNode depNode;
            if (depNodes.Count() > 0) {
                depNode = depNodes.First();
                if (depNode.ei_departmentAuditUser.Count() > 0 && string.IsNullOrEmpty(nodeName)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "存在审核人的情况下不能设置节点名称为空" });
                }
            }
            else {
                depNode = new ei_departmentAuditNode();
                depNode.ei_department = dep;
                depNode.FIsCounterSign = false;
                depNode.FProcessName = "请假";
                db.ei_departmentAuditNode.Add(depNode);
            }
            depNode.FAuditNodeName = nodeName;

            db.SaveChanges();
            WriteEventLog("部门管理", depNum+";变更名称：" + nodeName);
            return Json(new SimpleResultModel() { suc = true });

        }

        [SessionTimeOutJsonFilter]
        public JsonResult ToggleAuditCounterSign(string depNum)
        {
            var dep = db.ei_department.Single(e => e.FNumber == depNum);
            var depNodes = dep.ei_departmentAuditNode.ToList();
            ei_departmentAuditNode depNode;
            if (depNodes.Count() > 0) {
                depNode = depNodes.First();
            }
            else {
                depNode = new ei_departmentAuditNode();
                depNode.ei_department = dep;
                depNode.FAuditNodeName = "";
                depNode.FProcessName = "请假";
                db.ei_departmentAuditNode.Add(depNode);
            }
            depNode.FIsCounterSign = depNode.FIsCounterSign == true ? false : true;

            db.SaveChanges();
            WriteEventLog("部门管理", depNum + ";切换是否会签");
            return Json(new SimpleResultModel() { suc = true });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult SaveDepAuditor(string depNum, int depAuditorId, string auditor, string bTime, string eTime)
        {
            if (DateTime.Parse(bTime) > DateTime.Parse(eTime)) {
                return Json(new SimpleResultModel() { suc = false, msg = "生效日期不能晚于失效日期" });
            }
            string auditorNumber = GetUserCardByNameAndCardNum(auditor);
            //if (string.IsNullOrEmpty(GetUserEmailByCardNum(auditorNumber))) {
            //    return Json(new SimpleResultModel() { suc = false, msg = "此审核人没有登记邮箱，不能设置" });
            //}

            ei_departmentAuditUser depAuditor;
            
            if (depAuditorId == 0) {
                var auditNode = db.ei_department.Single(d => d.FNumber == depNum).ei_departmentAuditNode.First();
                depAuditor = new ei_departmentAuditUser();
                depAuditor.ei_departmentAuditNode = auditNode;
                db.ei_departmentAuditUser.Add(depAuditor);
            }
            else {
                depAuditor = db.ei_departmentAuditUser.Single(d => d.id == depAuditorId);
            }
            depAuditor.FAuditorNumber = auditorNumber;
            depAuditor.FBeginTime = DateTime.Parse(bTime);
            depAuditor.FEndTime = DateTime.Parse(eTime);

            db.SaveChanges();
            return Json(new SimpleResultModel() { suc = true, extra = depAuditor.id.ToString() });

        }

        [SessionTimeOutJsonFilter]
        public JsonResult LoadDepAuditors(string depNum)
        {
            var dep = db.ei_department.Single(d => d.FNumber == depNum);
            if (dep.ei_departmentAuditNode.Count() == 0) {
                return Json(new { suc = false });
            }
            var node = dep.ei_departmentAuditNode.First();
            if (node.ei_departmentAuditUser.Count() == 0) {
                return Json(new { suc = false });
            }
            var list = (from u in node.ei_departmentAuditUser
                        where u.isDeleted == null || u.isDeleted == false
                        orderby u.FEndTime descending
                        select new
                        {
                            id = u.id,
                            auditorNum = u.FAuditorNumber,
                            auditorName = GetUserNameByCardNum(u.FAuditorNumber),
                            bTime = ((DateTime)u.FBeginTime).ToString("yyyy-MM-dd"),
                            eTime = ((DateTime)u.FEndTime).ToString("yyyy-MM-dd"),
                            isExpire = u.FEndTime < DateTime.Now
                        }).ToList();
            return Json(new { suc = true, list = list });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult RemoveDepAuditor(int depAuditorId)
        {
            var depAuditors = db.ei_departmentAuditUser.Where(d => d.id == depAuditorId);
            
            if(depAuditors.Count()>0){
                depAuditors.First().isDeleted = true;                
                db.SaveChanges();
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "此审核人不存在" });
            }
            WriteEventLog("部门管理", "删除审核人：" + depAuditorId);
            return Json(new SimpleResultModel() { suc = true });
        }

        public string InitDepartment()
        {
            ei_department dep = new ei_department();
            dep.FName = "信利集团";
            dep.FNumber = "1";
            dep.FAdmin = "110428101";
            dep.FCreator = "110428101";
            dep.FCreateDate = DateTime.Now;

            db.ei_department.Add(dep);

            ei_department dep2 = new ei_department();
            dep2.FName = "信利光电股份有限公司";
            dep2.FNumber = "2";
            dep2.FAdmin = "110428101";
            dep2.FCreator = "110428101";
            dep2.FCreateDate = DateTime.Now;

            db.ei_department.Add(dep2);

            string[] names = new string[] { "信利半导体有限公司","信利工业有限公司","信利仪器有限公司","信利工业有限公司","信息中心","采购中心","会计部","行政部" };
            for(int i=1;i<=names.Length;i++){
                var d = new ei_department();
                d.FName = names[i-1];
                d.FNumber = "1" + i.ToString().PadLeft(2, '0');
                d.FAdmin = "110428101";
                d.FCreator = "110428101";
                d.FCreateDate = DateTime.Now;
                d.FParent = "1";

                db.ei_department.Add(d);
            }
            db.SaveChanges();
            return "ok";
        }

        [AuthorityFilter]
        public ActionResult UpdateHREmpDept()
        {
            return View();
        }

        public JsonResult BeginUpdateHREmpDept(string cardNumber, int depId, DateTime inDate, string position)
        {
            if (string.IsNullOrWhiteSpace(cardNumber)) {
                return Json(new SimpleResultModel() { suc = false, msg = "厂牌编号必须填写" });
            }
            if (string.IsNullOrWhiteSpace(position)) {
                return Json(new SimpleResultModel() { suc = false, msg = "调入部门岗位必须填写" });
            }

            try {
                new SJSv().UpdateHREmpDept(cardNumber, depId, inDate, position);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败：" + ex.Message });
            }
            WriteEventLog("员工调动", cardNumber + ":" + depId.ToString() + ";" + inDate.ToShortDateString() + ";" + position);
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });

        }

        #endregion

        #region 审核人与通知人设置

        [AuthorityFilter]
        public ActionResult AuditorSetting()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetFlowAuditors()
        {
            var result = (from a in db.flow_auditorRelation
                          join u in db.ei_users on a.relate_value equals u.card_number
                          orderby a.bill_type, a.relate_name, a.relate_text
                          select new
                          {
                              a.id,
                              a.relate_value,
                              u.name,
                              a.bill_type,
                              a.relate_name,
                              a.relate_text
                          }).ToList();
            return Json(result,JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveFlowAuditors(string obj)
        {
            flow_auditorRelation f = JsonConvert.DeserializeObject<flow_auditorRelation>(obj);

            if (f.bill_type == null || f.relate_name == null || f.relate_value == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "表单值不完整" }); 
            }

            f.relate_value = GetUserCardByNameAndCardNum(f.relate_value);

            if (f.id == 0) {
                db.flow_auditorRelation.Add(f);
            }
            else {
                var currentF = db.flow_auditorRelation.Single(a => a.id == f.id);
                MyUtils.CopyPropertyValue(f, currentF);
            }

            try {
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true });

        }

        public JsonResult RemoveFlowAudiors(int id)
        {
            try {
                var f = db.flow_auditorRelation.Single(a => a.id == id);
                db.flow_auditorRelation.Remove(f);
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult GetFlowNotifiers()
        {
            var result = db.flow_notifyUsers.OrderBy(f => f.bill_type).ThenBy(f => f.cond1).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveFlowNotifiers(string obj)
        {
            flow_notifyUsers f = JsonConvert.DeserializeObject<flow_notifyUsers>(obj);

            if (f.bill_type == null || f.card_number == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "表单值不完整" });
            }

            f.name = GetUserNameByNameAndCardNum(f.card_number);
            f.card_number = GetUserCardByNameAndCardNum(f.card_number);

            if (f.id == 0) {
                db.flow_notifyUsers.Add(f);
            }
            else {
                var currentF = db.flow_notifyUsers.Single(a => a.id == f.id);
                MyUtils.CopyPropertyValue(f, currentF);
            }

            try {
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true });

        }

        public JsonResult RemoveFlowNotifiers(int id)
        {
            try {
                var f = db.flow_notifyUsers.Single(a => a.id == id);
                db.flow_notifyUsers.Remove(f);
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true });
        }

        #endregion
    }
}
