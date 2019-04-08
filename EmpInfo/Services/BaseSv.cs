using EmpInfo.Models;
using System;
using System.Linq;

namespace EmpInfo.Services
{
    public class BaseSv
    {
        public ICAuditEntities db;

        public BaseSv()
        {
            db = new ICAuditEntities();
        }

        public void WriteEventLog(ei_event_log log)
        {
            db.ei_event_log.Add(log);
            db.SaveChanges();
        }

        //是否拥有某权限
        protected bool HasGotPower(string powerName, int userId)
        {            
            var powers = (from g in db.ei_groups
                          from a in g.ei_groupAuthority
                          from gu in g.ei_groupUser
                          where gu.user_id == userId
                          && a.ei_authority.en_name == powerName
                          select a).ToList();
            if (powers.Count() > 0) {
                return true;
            }
            return false;
        }

        //根据厂牌获取姓名(厂牌)
        protected string GetUserNameAndCardByCardNum(string cardNumbers)
        {
            if (string.IsNullOrEmpty(cardNumbers)) return "";
            string names = "";
            foreach (var num in cardNumbers.Split(new char[] { ';', '；' })) {
                if (!string.IsNullOrEmpty(num)) {
                    var emp = db.vw_getAllhrEmp.Where(v => v.emp_no == num).ToList();
                    if (emp.Count() > 0) {
                        if (!string.IsNullOrEmpty(names)) names += ";";
                        names += emp.First().emp_name + "(" + num + ")";
                    }
                }
            }
            return names;
        }

        //根据厂牌获取姓名
        protected string GetUserNameByCardNum(string cardNumbers)
        {
            if (string.IsNullOrEmpty(cardNumbers)) return "";
            string names = "";
            foreach (var num in cardNumbers.Split(new char[] { ';', '；' })) {
                if (!string.IsNullOrEmpty(num)) {
                    var emp = db.vw_getAllhrEmp.Where(v => v.emp_no == num).ToList();
                    if (emp.Count() > 0) {
                        if (!string.IsNullOrEmpty(names)) names += ";";
                        names += emp.First().emp_name;
                    }
                }
            }
            return names;
        }

        //根据姓名(厂牌)取得厂牌
        protected string GetUserCardByNameAndCardNum(string nameAndCards)
        {
            if (string.IsNullOrEmpty(nameAndCards)) return "";
            string cards = "";
            foreach (var num in nameAndCards.Split(new char[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (!string.IsNullOrEmpty(cards)) cards += ";";
                cards += num.Split(new char[] { '(', ')' })[1];
            }
            return cards;
        }

        //根据厂牌获取邮箱
        protected string GetUserEmailByCardNum(string cardNumbers)
        {
            string emails = "";
            foreach (var num in cardNumbers.Split(new char[] { ';', '；' })) {
                if (!string.IsNullOrEmpty(num)) {
                    var user = db.ei_users.Where(u => u.card_number == num);
                    if (user.Count() > 0) {
                        if (!string.IsNullOrEmpty(emails)) emails += ",";
                        emails += user.First().email;
                    }
                }
            }
            return emails;
        }

        //根据部门编码获得部门长名称
        protected string GetDepLongNameByNum(string depNum)
        {
            var dep = db.ei_department.Single(d => d.FNumber == depNum);
            if (dep.FIsDeleted == true) {
                return "已删除部门";
            }

            string depName = dep.FName;
            while (dep.FParent != null) {
                dep = db.ei_department.Single(d => d.FNumber == dep.FParent);
                if (dep.FIsForbit == true) {
                    return "已禁用部门";
                }
                else {
                    depName = dep.FName + "/" + depName;
                }
            }

            return depName;
        }

    }
}