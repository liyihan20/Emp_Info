using EmpInfo.FlowSvr;
using EmpInfo.Models;
using EmpInfo.QywxWebSrv;
using EmpInfo.Util;
using System;
using System.Collections.Generic;
using System.Linq;


namespace EmpInfo.Services
{
    public abstract class BillSv : BaseSv
    {
        // --------------------------   抽象属性   -------------------------- //

        /// <summary>
        /// 单据类型EN
        /// </summary>
        public abstract string BillType { get; }

        /// <summary>
        /// 单据类型名
        /// </summary>
        public abstract string BillTypeName { get; }
        


        // --------------------------   抽象方法   -------------------------- //

        /// <summary>
        /// 申请时需要带到申请单的信息
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public abstract object GetInfoBeforeApply(UserInfo userInfo,UserInfoDetail userInfoDetail);

        /// <summary>
        /// 保存申请
        /// </summary>
        /// <param name="fc"></param>
        /// <param name="userInfo"></param>
        public abstract void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo);

        /// <summary>
        /// 获取单据对象
        /// </summary>
        /// <returns></returns>
        public abstract object GetBill();

        /// <summary>
        /// 审核申请
        /// </summary>
        /// <param name="fc"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public abstract SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo);

        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="model"></param>
        public abstract void SendNotification(FlowResultModel model);

        // --------------------------  虚方法  -------------------------- //

        /// <summary>
        /// 新建单据视图名
        /// </summary>
        /// <returns></returns>
        public virtual string CreateViewName()
        {
            return "BeginApply" + BillType;
        }

        /// <summary>
        /// 查看单据视图名
        /// </summary>
        /// <returns></returns>
        public virtual string CheckViewName()
        {
            return "Check" + BillType + "Apply"; 
        }

        /// <summary>
        /// 我申请的
        /// </summary>
        /// <returns></returns>
        public virtual string GetMyAppliesViewName()
        {
            return "GetMyApplyList";
        }

        /// <summary>
        /// 审核申请
        /// </summary>
        /// <returns></returns>
        public virtual string AuditViewName()
        {
            return "BeginAuditApply";
        }

        public virtual string MyAuditingViewName()
        {
            return "GetMyAuditingList";
        }

        /// <summary>
        /// 菜单项
        /// </summary>
        /// <returns></returns>
        public virtual List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var list = new List<ApplyMenuItemModel>();
            list.Add(new ApplyMenuItemModel()
            {
                url = "BeginApply?billType=" + BillType,
                text = "开始申请",
                iconFont = "fa-pencil",
                colorClass = "text-danger"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyApplyList?billType=" + BillType,
                text = "我申请的",
                iconFont = "fa-th"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditingList?billType=" + BillType,
                text = "我的待办",
                iconFont = "fa-th-list"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditedList?billType=" + BillType,
                text = "我的已办",
                iconFont = "fa-th-large"
            });

            return list;
        }

        /// <summary>
        /// 获取导航栏的链接，在html页面需要配合@Url.Content("~")使用，默认所有的申请都是在智慧办公集合以下，如不是，需要覆盖这个方法
        /// </summary>
        /// <returns></returns>
        public virtual List<ApplyNavigatorModel> GetApplyNavigatorLinks()
        {
            return new List<ApplyNavigatorModel>(){
                new ApplyNavigatorModel(){
                    text="智慧办公集合",
                    url="Home/WorkGroupIndex"
                }
            };
        }

        //获取流水号
        public virtual string GetNextSysNum(string billType, int digitPerDay = 3)
        {
            string dateStr = DateTime.Now.ToString("yyMMdd");
            string result = billType + dateStr;
            var maxNumRecords = db.all_maxNum.Where(a => a.bill_type == billType && a.date_str == dateStr);
            if (maxNumRecords.Count() > 0) {
                var maxNumRecord = maxNumRecords.First();
                result += string.Format("{0:D" + digitPerDay + "}", maxNumRecord.max_num);
                maxNumRecord.max_num = maxNumRecord.max_num + 1;
            }
            else {
                var maxNumRecord = new all_maxNum();
                maxNumRecord.bill_type = billType;
                maxNumRecord.date_str = dateStr;
                maxNumRecord.max_num = 2;
                db.all_maxNum.Add(maxNumRecord);

                result += string.Format("{0:D" + digitPerDay + "}", 1);
            }

            db.SaveChanges();

            return result;
        }

        /// <summary>
        /// 撤销流程
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="sysNo"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public virtual SimpleResultModel AbortApply(UserInfo userInfo, string sysNo, string reason = "")
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;

            if (flow.ApplyHasBeenAudited(sysNo)) {
                var auditStatus = flow.GetApplyResult(sysNo);
                return new SimpleResultModel() { suc = false, msg = "不能撤销，当前状态是：" + auditStatus };
            }
            else {
                //流程还未审批可以直接撤销
                result = flow.AbortFlow(userInfo.cardNo, sysNo);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        /// <summary>
        /// 流转记录
        /// </summary>
        /// <param name="sysNo"></param>
        /// <returns></returns>
        public virtual List<FlowRecordModels> CheckFlowRecord(string sysNo)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetFlowRecord(sysNo).ToList();
            result.ForEach(r => r.auditors = GetUserNameByCardNum(r.auditors));

            if (flow.GetApplyResult(sysNo) == "已通过") {
                result.Add(new FlowRecordModels()
                {
                    stepName = "完成申请",
                    auditors = "系统",
                    auditResult = "成功",
                    auditTimes = result.OrderByDescending(r => r.auditTimes).First().auditTimes,
                    opinions = ""
                });
            }
            return result;
        }

        public virtual string GetAuditStatus(string sysNo)
        {
            return new FlowSvrSoapClient().GetApplyResult(sysNo);
        }

        public virtual List<FlowAuditListModel> OrderAuditList(List<FlowAuditListModel> list){
            return list.OrderBy(l => l.applyTime).ToList();
        }

        /// <summary>
        /// 获取审核列表比较器
        /// </summary>
        /// <returns></returns>
        //public virtual IComparer<FlowAuditListModel> GetAuditListComparer()
        //{
        //    return new BillComparer();
        //}

        /// <summary>
        /// 自定义审核列表比较器
        /// </summary>
        //private class BillComparer : IComparer<FlowAuditListModel>
        //{

        //    public int Compare(FlowAuditListModel x, FlowAuditListModel y)
        //    {
        //        if (x == null && y == null) return 0;
        //        if (x == null) return -1;
        //        if (y == null) return 1;
        //        if (x.applyTime == null) return -1;
        //        if (y.applyTime == null) return -1;
        //        return x.applyTime > y.applyTime ? 1 : -1;
        //    }
        //}

        /// <summary>
        /// 能否访问这个功能
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public virtual bool CanAccessApply(UserInfo userInfo)
        {
            return true;
        }        

        /// <summary>
        /// 流程完结的邮件通知
        /// </summary>
        /// <param name="sysNo">流水号</param>
        /// <param name="subject">邮件标题</param>
        /// <param name="names">称呼</param>
        /// <param name="notification">通知内容</param>
        /// <param name="emails">收件人</param>
        /// <param name="ccEmails">cc收件人</param>
        /// <param name="url">链接，不提供就按照默认链接</param>
        /// <returns></returns>
        public bool SendEmailForCompleted(string sysNo, string subject, string names, string notification, string emails, string ccEmails="", string url = "")
        {
            string content = "<div>" + names + ",你好：</div>";
            content += "<div style='margin-left:30px;'>" + notification + "</div>";
            content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
            if (string.IsNullOrEmpty(url)) {
                url = "Apply/CheckApply?sysNo=" + sysNo;
            }
            content += string.Format("<div><a href='http://192.168.90.100/Emp/{0}'>内网用户点击此链接</a></div>", url);
            content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/{0}'>外网用户点击此链接</a></div></div>", url);

            return MyEmail.SendEmail(subject, emails, content, ccEmails); 
        }

        /// <summary>
        /// 发送给审批人的通知
        /// </summary>
        /// <param name="sysNo">流水号</param>
        /// <param name="nextStep">下一步骤</param>
        /// <param name="subject">邮件标题</param>
        /// <param name="names">称呼</param>
        /// <param name="notification">通知内容</param>
        /// <param name="emails">收件人</param>
        /// <param name="ccEmails">cc收件人</param>
        /// <param name="url">链接，不提供就按照默认链接</param>
        /// <returns></returns>
        public bool SendEmailToNextAuditor(string sysNo, int nextStep, string subject, string names, string notification, string emails, string ccEmails = "", string url = "")
        {
            string content = "<div>" + names + ",你好：</div>";
            content += "<div style='margin-left:30px;'>" + notification + "</div>";
            content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
            if (string.IsNullOrEmpty(url)) {
                url = string.Format("Apply/BeginAuditApply?sysNo={0}&step={1}", sysNo, nextStep);
            }
            content += string.Format("<div><a href='http://192.168.90.100/Emp/{0}'>内网用户点击此链接</a></div>", url);
            content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/{0}'>外网用户点击此链接</a></div></div>", url);

            return MyEmail.SendEmail(subject, emails, content, ccEmails);
        }

        /// <summary>
        /// 流程完成，发送微信推送给申请人
        /// </summary>
        /// <param name="processName">流程名称</param>
        /// <param name="sysNo">流程编号</param>
        /// <param name="result">处理结果</param>
        /// <param name="pushUser">推送用户</param>
        //public void SendWxMessageForCompleted(string processName, string sysNo, string result, vw_push_users pushUser)
        //{
        //    if (pushUser == null) {
        //        return;
        //    }
        //    SendWxMessageForCompleted(processName, sysNo, result, new List<vw_push_users>() { pushUser });
        //}

        public void SendQywxMessageForCompleted(string processName, string sysNo, string result, List<string> cardNumberList)
        {
            string cardNumber = string.Join("|", cardNumberList);
            string url = "";
            TextCardMsg msg = new TextCardMsg();
            msg.touser = cardNumber;
            msg.textcard = new TextCardContent();
            msg.textcard.title = "你有一张申请单已审批完成";
            msg.textcard.description = " <div class=\"highlight\">流程名称：" + processName + "</div>";
            msg.textcard.description += "<div class=\"highlight\">流程编号：" + sysNo + "</div>";
            msg.textcard.description += "<div class=\"highlight\">审批结果：" + result + "</div>";
            msg.textcard.description += "<div class=\"highlight\">办结时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "</div>";
            url = "http://emp.truly.com.cn/emp/QYWX/Login?returnUrl=http://emp.truly.com.cn/emp/Apply/CheckApply?sysNo=" + sysNo;
            msg.textcard.url = GetQYWXOAthLink(url);

            SendQYWXCardMsg(msg);
            
        }
        //public void SendWxMessageForCompleted(string processName, string sysNo, string result, List<vw_push_users> pushUsers)
        //{
        //    foreach (var pushUser in pushUsers) {
        //        wx_pushMsg pm = new wx_pushMsg();
        //        pm.FCardNumber = pushUser.card_number;
        //        pm.FFirst = "你有一张申请单已审批完成";
        //        pm.FHasSend = false;
        //        pm.FInTime = DateTime.Now;
        //        pm.FkeyWord1 = processName;
        //        pm.FKeyWord2 = sysNo;
        //        pm.FKeyWord3 = result;
        //        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        //        pm.FOpenId = pushUser.wx_openid;
        //        pm.FPushType = "办结";
        //        pm.FRemark = "点击可查看详情";
        //        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckApply&param={2}", pushUser.card_number, MyUtils.getMD5(pushUser.card_number), sysNo);
        //        db.wx_pushMsg.Add(pm);
        //    }
        //    db.SaveChanges();
        //}

        /// <summary>
        /// 发送通知审核的微信给审核人，统一用【待审2】模板
        /// </summary>
        /// <param name="processName">流程名称</param>
        /// <param name="sysNo">流程编号</param>
        /// <param name="step">审核步骤</param>
        /// <param name="auditStepName">步骤名称</param>
        /// <param name="applierName">申请人</param>
        /// <param name="applierTime">申请时间</param>
        /// <param name="applyContent">申请内容</param>
        /// <param name="pushUsers">审核人list</param>
        //public void SendWxMessageToNextAuditor(string processName, string sysNo, int step, string auditStepName, string applierName, string applierTime, string applyContent, List<vw_push_users> pushUsers)
        //{
        //    if (pushUsers.Count()==0) {
        //        return;
        //    }
        //    foreach (var u in pushUsers) {
        //        wx_pushMsg pm = new wx_pushMsg();
        //        pm.FCardNumber = u.card_number;
        //        pm.FFirst = "你有一张待审批事项：" + sysNo;
        //        pm.FHasSend = false;
        //        pm.FInTime = DateTime.Now;
        //        pm.FkeyWord1 = processName;
        //        pm.FKeyWord2 = applierName;
        //        pm.FKeyWord3 = applierTime;
        //        pm.FKeyWord4 = applyContent;
        //        pm.FKeyWord5 = auditStepName;
        //        pm.FOpenId = u.wx_openid;
        //        pm.FPushType = "待审2";
        //        pm.FRemark = "点击可进入审批此申请";
        //        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditApply&param={2}", u.card_number, MyUtils.getMD5(u.card_number), sysNo + "%3B" + step); //%3B是分号;
        //        db.wx_pushMsg.Add(pm);
        //    }
        //    db.SaveChanges();
        //}

        public void SendQywxMessageToNextAuditor(string processName, string sysNo, int step, string auditStepName, string applierName, string applierTime, string applyContent, List<string> cardNumberList)
        {
            string cardNumber = string.Join("|", cardNumberList);
            string url = "";
            TextCardMsg msg = new TextCardMsg();
            msg.touser = cardNumber;
            msg.textcard = new TextCardContent();
            msg.textcard.title = "你有一张待处理事项";
            msg.textcard.description = " <div class=\"highlight\">流程名称：" + processName + "</div>";
            msg.textcard.description += "<div class=\"highlight\">流程编号：" + sysNo + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请人：" + applierName + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请时间：" + applierTime + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请内容：" + applyContent + "</div>";
            msg.textcard.description += "<div class=\"highlight\">处理节点：" + auditStepName + "</div>";
            url = "http://emp.truly.com.cn/emp/QYWX/Login?returnUrl=" + Uri.EscapeDataString("http://emp.truly.com.cn/emp/Apply/BeginAuditApply?sysNo=" + sysNo + "&step=" + step);
            
            msg.textcard.url = GetQYWXOAthLink(url);

            SendQYWXCardMsg(msg);
        }

        /// <summary>
        /// 抄送给某人，流程未结束时
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="sysNo"></param>
        /// <param name="applierName"></param>
        /// <param name="applierTime"></param>
        /// <param name="applyContent"></param>
        /// <param name="cardNumberList"></param>
        public void SendQywxMessageToCC(string processName, string sysNo, string applierName, string applierTime, string applyContent, List<string> cardNumberList)
        {
            string cardNumber = string.Join("|", cardNumberList);
            string url = "";
            TextCardMsg msg = new TextCardMsg();
            msg.touser = cardNumber;
            msg.textcard = new TextCardContent();
            msg.textcard.title = "你有一张抄送给你的申请单";
            msg.textcard.description = " <div class=\"highlight\">流程名称：" + processName + "</div>";
            msg.textcard.description += "<div class=\"highlight\">流程编号：" + sysNo + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请人：" + applierName + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请时间：" + applierTime + "</div>";
            msg.textcard.description += "<div class=\"highlight\">申请内容：" + applyContent + "</div>";
            url = "http://emp.truly.com.cn/emp/QYWX/Login?returnUrl=" + Uri.EscapeDataString("http://emp.truly.com.cn/emp/Apply/CheckApply?sysNo=" + sysNo);

            msg.textcard.url = GetQYWXOAthLink(url);

            SendQYWXCardMsg(msg);
        }

        /// <summary>
        /// 行政部发送约谈信息
        /// </summary>
        /// <param name="sysNo"></param>
        /// <param name="recevierNum"></param>
        /// <param name="recevierDepName"></param>
        /// <param name="senderName"></param>
        /// <param name="bookTime"></param>
        public void AHPushMsg(string sysNo,string recevierNum,string recevierName, string recevierDepName, string senderName, string bookTime,string pushMsg)
        {
            DateTime dt;
            if (!DateTime.TryParse(bookTime, out dt)) {
                throw new Exception("预约时间不是日期格式");
            }

            ei_leaveDayExceedPushLog log = new ei_leaveDayExceedPushLog();
            log.book_date = dt;
            log.send_date = DateTime.Now;
            log.send_user = senderName;
            log.sys_no = sysNo;
            db.ei_leaveDayExceedPushLog.Add(log);

            var receviers = recevierNum;
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var step1Auditors = flow.GetCertainStepAuditors(sysNo, 1);
            if (!string.IsNullOrEmpty(step1Auditors)) {
                receviers += ";" + step1Auditors;
            }

            string addr, phone;
            if (recevierDepName.StartsWith("惠州")) {
                addr = "研发楼一楼行政及人力资源部";
                phone = "0752-6568888/7888";
            }
            else if (recevierDepName.Contains("光电仁寿")) {
                addr = "行政部";
                phone = "";
            }
            else {
                addr = "写字楼（行政大楼）一楼行政部";
                phone = "3380015-3003";
            }

            //发微信,改到企业微信
            TextMsg msg = new TextMsg();
            msg.touser = receviers.Replace(";", "|");
            msg.text = new TextContent();
            msg.text.content = string.Format("{0}，请您于下述时间，前往{2}与{1}面谈。", recevierName, senderName, addr);
            msg.text.content += "\n面谈内容： " + pushMsg;
            msg.text.content += "\n预约时间： " + dt.ToString("yyyy-MM-dd HH:mm");
            msg.text.content += "\n请您准时到达，如有疑问,请致电行政部" + senderName + "，电话：" + phone;
            SendQYWXMsg(msg);

            //foreach (var ad in receviers.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
            //    var pushUsers = db.vw_push_users.Where(u => u.card_number == ad).ToList();
            //    if (pushUsers.Count() == 0) {
            //        continue;
            //    }
            //    var pushUser = pushUsers.First();
            //    wx_pushMsg pm = new wx_pushMsg();
            //    pm.FCardNumber = ad;
            //    pm.FFirst = string.Format("{0}，请您于下述时间，前往{2}与{1}面谈。", recevierName, senderName, addr);
            //    pm.FHasSend = false;
            //    pm.FInTime = DateTime.Now;
            //    pm.FkeyWord1 = pushMsg;
            //    pm.FKeyWord2 = dt.ToString("yyyy-MM-dd HH:mm");
            //    pm.FOpenId = pushUser.wx_openid;
            //    pm.FPushType = "行政面谈";
            //    pm.FRemark = "请您准时到达，如有疑问。请致电行政部" + senderName + "，电话：" + phone;
            //    db.wx_pushMsg.Add(pm);
            //}
            db.SaveChanges();

            //发送邮件
            string subject = "行政部面谈通知";
            string emailAddrs = GetUserEmailByCardNum(recevierNum);
            string ccEmails = GetUserEmailByCardNum(step1Auditors);
            string content = string.Format("<div>{0}，你好！</div><div style='margin-left:30px;'>请您于下述时间，前往{1}与{2}面谈。<br /> 面谈内容：{3} <br/> 预约时间：{4:yyyy-MM-dd HH:mm}</div>", recevierName, addr, senderName, pushMsg, dt);
            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }        

        public ei_leaveDayExceedPushLog GetAHMsgPushLog(string sysNo)
        {
            return db.ei_leaveDayExceedPushLog.Where(l => l.sys_no == sysNo).OrderByDescending(l => l.id).FirstOrDefault();
        }

        public void SendQYWXMsg(TextMsg msg, DateTime? whenToPush = null)
        {
            msg.agentid = "1000007";
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            wx.PushTextMsg("wZRxdsuqeFAqJDG7VLaCTkImfsuce0qwyLO3ksBUkMY", "李逸焊", msg, whenToPush);
        }

        public void SendQYWXCardMsg(TextCardMsg msg, DateTime? whenToPush = null)
        {
            msg.agentid = "1000007";
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();           
            wx.PushTextCardMsg("wZRxdsuqeFAqJDG7VLaCTkImfsuce0qwyLO3ksBUkMY", "李逸焊", msg, whenToPush);
        }

        public string GetQYWXOAthLink(string url)
        {
            QywxApiSrvSoapClient client = new QywxApiSrvSoapClient();
            return client.GetOAthLink(url);
        }

    }
}