using EmpInfo.Filter;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace EmpInfo.Controllers
{    
    public class RestaurantController : RestaurantBaseController
    {
        //获取食堂参数
        private string GetParam(string paramName)
        {
            string result = "";
            var items = db.dn_items.Where(i => i.name == paramName && i.res_no == currentResNo);
            if (items.Count() > 0) {
                result = items.First().value;
            }
            return result;
        }

        //获取所有食堂
        [SessionTimeOutJsonFilter]
        public JsonResult GetAllRes()
        {
            var result = (from r in db.dn_Restaurent
                          select new ResAndDishCountModel()
                          {
                              resName = r.name,
                              resNo = r.no
                          }).ToList();
            result.ForEach(r => r.dishCount = db.dn_dishes.Where(d => d.res_no == r.resNo && d.is_selling == true).Count());

            return Json(new { result = result });
        }

        [SessionTimeOutFilter]
        public ActionResult SetCurrentRes(string resNo)
        {
            try {
                currentResNo = resNo;
            }
            catch {
                ViewBag.tip = "食堂进入失败";
                return View("Error");
            }
            WriteEventLog("进入食堂", "食堂编码：" + resNo);
            return RedirectToAction("ResIndex");
        }

        //会所在线点餐系统
        [SessionTimeOutFilter]
        public ActionResult ResIndex()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }

            //推荐
            int[] id = db.dn_dishes.Where(d => d.is_on_top == true && d.res_no == currentResNo).Select(d => d.id).ToArray();

            //菜式
            var dishes = (from d in db.dn_dishes
                          where d.is_selling == true
                          && d.res_no == currentResNo
                          orderby d.number
                          select new SimpleDishModel()
                          {
                              id = d.id,
                              type = d.type,
                              name = d.name,
                              price = d.price,
                              sell_weekday = d.sell_weekday,
                              sell_time = d.sell_time,
                              is_on_top = d.is_on_top
                          }).ToList();

            ViewData["topIds"] = id;
            ViewData["dishes"] = dishes;

            WriteEventLog("点餐主界面", "打开主界面");
            return View();
        }

        //搜索菜式
        //public JsonResult SearchDishes(string value)
        //{
        //    if (string.IsNullOrEmpty(value)) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "请输入搜索内容" });
        //    }
        //    var result = (from d in db.dn_dishes
        //                  where d.is_selling == true
        //                  && d.name.Contains(value)
        //                  orderby d.number
        //                  select new SimpleDishModel()
        //                  {
        //                      id = d.id,
        //                      name = d.name,
        //                      type = d.type,
        //                      sell_time = d.sell_time,
        //                      sell_weekday = d.sell_weekday,
        //                      price = d.price
        //                  }).ToList();
        //    if (result.Count() == 0) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "没有搜索到符合条件的菜式" });
        //    }
        //    return Json(new { suc = true, result = result });
        //}

        [SessionTimeOutJsonFilter]
        public JsonResult SearchThings(string query)
        {
            var result = (from d in db.dn_dishes
                          where d.is_selling == true
                          && (d.name.Contains(query)
                          || d.number.Contains(query))
                          && d.res_no == currentResNo
                          orderby d.type, d.number
                          select new
                          {
                              data = d.id,
                              value = d.name
                          }).Take(10).ToList();
            WriteEventLog("搜索菜式", "内容：" + query);
            return Json(new { suggestions = result });
        }

        //获取菜式的图片缩略图
        public void GetThumbPic(int id, int which = 1)
        {
            try {
                byte[] image = null;
                switch (which) {
                    case 1:
                        image = db.dn_dishes.Single(d => d.id == id).image_1_thumb;
                        break;
                    case 2:
                        image = db.dn_dishes.Single(d => d.id == id).image_2_thumb;
                        break;
                    case 3:
                        image = db.dn_dishes.Single(d => d.id == id).image_3_thumb;
                        break;
                }
                if (image != null)
                    Response.BinaryWrite(image.ToArray());
            }
            catch (Exception) {
                throw;
            }
        }

        //获取正式图片
        public void GetFormalPic(int id, int which)
        {
            try {
                byte[] image = null;
                switch (which) {
                    case 1:
                        image = db.dn_dishes.Single(d => d.id == id).image_1;
                        break;
                    case 2:
                        image = db.dn_dishes.Single(d => d.id == id).image_2;
                        break;
                    case 3:
                        image = db.dn_dishes.Single(d => d.id == id).image_3;
                        break;
                }
                if (image != null)
                    Response.BinaryWrite(image.ToArray());
            }
            catch (Exception) {
                throw;
            }
        }

        //菜式详细页面
        [SessionTimeOutFilter]
        public ActionResult DishDetail(int id)
        {
            try {
                DishDetailModel dish = (from d in db.dn_dishes
                                        where d.id == id
                                        select new DishDetailModel()
                                        {
                                            id = d.id,
                                            number = d.number,
                                            name = d.name,
                                            price = d.price,
                                            can_delivery = d.can_delivery,
                                            description = d.description,
                                            is_on_top = d.is_on_top,
                                            is_selling = d.is_selling,
                                            sell_time = d.sell_time,
                                            sell_weekday = d.sell_weekday,
                                            type = d.type,
                                            has_image_1 = d.image_1 == null ? false : true,
                                            has_image_2 = d.image_2 == null ? false : true,
                                            has_image_3 = d.image_3 == null ? false : true
                                        }).First();
                ViewData["dish"] = dish;
            }
            catch (Exception) {
                ViewBag.tip = "页面已失效";
                return View("Error");
            }
            WriteEventLog("菜式详情", "打开详情页面，id：" + id);
            return View();
        }

        //加入购物车
        [SessionTimeOutJsonFilter]
        public JsonResult AddIntoShoppingCar(int id, int qty)
        {
            if (qty < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "数量不能为负数" });
            }

            dn_dishes dish;
            try {
                dish = db.dn_dishes.Single(d => d.id == id);
            }
            catch {
                return Json(new SimpleResultModel() { suc = false, msg = "此商品不存在" });
                throw;
            }
            if (dish.is_selling == false) {
                return Json(new SimpleResultModel() { suc = false, msg = "此商品已下架" });
            }
            var cars = db.dn_shoppingCar.Where(s => s.user_no == userInfo.cardNo && s.dish_id == id && s.is_delete == null && s.is_submit == null && s.benefit_info == null && s.res_no == currentResNo);
            if (cars.Count() > 0) {
                var car = cars.First();
                car.qty = car.qty + qty;
                car.is_checked = true;
                db.SaveChanges();
                return Json(new SimpleResultModel() { suc = true, msg = "购物车已存在此商品，数量+" + qty });
            }
            var newCar = new dn_shoppingCar()
            {
                dish_id = id,
                user_no = userInfo.cardNo,
                in_time = DateTime.Now,
                qty = qty,
                is_checked = true,
                res_no = currentResNo
            };
            db.dn_shoppingCar.Add(newCar);
            db.SaveChanges();

            WriteEventLog("购物车", "加入购物车，id：" + id + ";qty:" + qty);
            return Json(new SimpleResultModel() { suc = true, msg = "成功加入购物车" });
        }

        //查看购物车
        [SessionTimeOutFilter]
        public ActionResult CheckShoppingCar()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }
            WriteEventLog("购物车", "查看购物车");
            return View();
        }

        //购物车的list
        private List<ShoppingCarModel> ShoppingCarList()
        {
            var cars = (from s in db.dn_shoppingCar
                        where s.user_no == userInfo.cardNo
                        && s.is_submit == null
                        && s.is_delete == null
                        && s.res_no == currentResNo
                        select new ShoppingCarModel()
                        {
                            car_id = s.id,
                            dish_id = s.dish_id,
                            is_selling = s.dn_dishes.is_selling,
                            name = s.dn_dishes.name,
                            price = s.benefit_price == null ? s.dn_dishes.price : s.benefit_price,
                            qty = s.qty,
                            is_checked = s.is_checked,
                            can_delivery = s.dn_dishes.can_delivery,
                            sell_day = s.dn_dishes.sell_weekday,
                            sell_time = s.dn_dishes.sell_time,
                            benefit_info = s.benefit_info
                        }).ToList();
            return cars;
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetShoppingCar()
        {
            return Json(ShoppingCarList());
        }

        //从购物车移除商品
        [SessionTimeOutJsonFilter]
        public JsonResult RemoveItemInShoppingCar(int id)
        {
            var cars = db.dn_shoppingCar.Where(d => d.user_no == userInfo.cardNo && d.id == id);
            if (cars.Count() == 1) {
                var car = cars.First();
                car.delete_time = DateTime.Now;
                car.is_delete = true;

                //积分换购的，需要返还积分
                if (car.benefit_info != null && car.benefit_info.Contains("积分换购")) {
                    int points = int.Parse(car.benefit_info.Split(':')[1]);
                    if (!ReturnPointsIfCanceled(points, "删除购物车")) {
                        return Json(new SimpleResultModel() { suc = false, msg = "删除失败，因为积分返还失败" });
                    }
                }

                //如果是生日餐，要将领取记录作废
                if (car.benefit_info != null && car.benefit_info.Contains("生日")) {
                    string thisYear = DateTime.Now.Year.ToString();
                    var birthdayMeals = db.dn_birthdayMealLog.Where(d => d.user_no == userInfo.cardNo && d.year == thisYear && d.is_cancelled != true);
                    if (birthdayMeals.Count() > 0) {
                        var bm = birthdayMeals.First();
                        bm.is_cancelled = true;
                    }
                }

                db.SaveChanges();
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "删除失败" });
            }
            WriteEventLog("购物车", "从购物车移除商品，id：" + id);
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //变更购物车数量
        [SessionTimeOutJsonFilter]
        public JsonResult ChangeItemQtyInCar(int id, int qty)
        {
            var cars = db.dn_shoppingCar.Where(d => d.user_no == userInfo.cardNo && d.id == id);
            if (cars.Count() == 1) {
                var car = cars.First();
                //如果有优惠，即积分换购或生日餐的，不允许变更数量
                if (car.benefit_info != null && qty != 1) {
                    return Json(new SimpleResultModel() { suc = false, msg = "优惠商品不允许更改数量" });
                }
                car.qty = qty;
                db.SaveChanges();
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败" });
            }
            WriteEventLog("购物车", "更改商品数量，id：" + id + ";qty:" + qty);
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //变更购物车选中状态
        [SessionTimeOutJsonFilter]
        public JsonResult ChangeItemCheckedInCar(int id, bool isChecked)
        {
            var cars = db.dn_shoppingCar.Where(d => d.user_no == userInfo.cardNo && d.id == id);
            if (cars.Count() == 1) {
                var car = cars.First();
                car.is_checked = isChecked;
                db.SaveChanges();
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败" });
            }
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //结算界面
        [SessionTimeOutFilter]
        public ActionResult MakeAnOrder()
        {
            var items = ShoppingCarList().Where(s => s.is_checked == true).ToList();
            ViewData["items"] = items;
            ViewData["userName"] = userInfo.name;
            ViewData["userPhone"] = userInfoDetail.phone;
            ViewData["shortPhone"] = userInfoDetail.shortPhone;

            //2016-10-05 加入部长点餐模块
            if (currentResNo.Equals("HS")) {
                var isMinister = HasGotPower("MinisterDishOrder");
                if (isMinister) {
                    WriteEventLog("会所订餐", "部长进入结算页面");
                    return View("MinisterMakeAnOrder");
                }
            }

            //获取参数：至少提前几分钟预约
            string beforeMinutes = GetParam("BeforeMinutesInt");
            if (string.IsNullOrEmpty(beforeMinutes)) {
                beforeMinutes = "0";
            }
            ViewData["beforeMinutes"] = int.Parse(beforeMinutes);

            WriteEventLog("会所订餐", "员工进入结算页面");
            return View();
        }
        //快速结算，不经过购物车
        [SessionTimeOutFilter]
        public ActionResult MakeAnOrderQuickly(int dishId)
        {
            //将购物车其它菜式设置勾选状态为false，然后将此菜式加入购物车
            var cars = db.dn_shoppingCar.Where(s => s.user_no == userInfo.cardNo && s.is_checked == true && s.is_submit == null && s.is_delete == null && s.res_no == currentResNo);
            foreach (var car in cars) {
                if (car.benefit_info == null && car.dish_id == dishId) {
                    car.delete_time = DateTime.Now;
                    car.is_delete = true;
                }
                else {
                    car.is_checked = false;
                }
            }
            db.SaveChanges();
            AddIntoShoppingCar(dishId, 1);
            WriteEventLog("购物车", "不经过购物车快速进入结算：dish_id:" + dishId);
            return RedirectToAction("MakeAnOrder");
        }

        //获取优惠信息
        [SessionTimeOutJsonFilter]
        public JsonResult GetBenefitInfos()
        {
            //优惠折扣信息
            var infos = (from i in db.dn_discountInfo
                         where i.for_everyone == true
                         && i.res_no == currentResNo
                         && i.end_date > DateTime.Now
                         && i.from_date <= DateTime.Now
                         select new
                         {
                             i.id,
                             i.discount_name,
                             i.discount_rate,
                             i.resume_bigger_than,
                             i.minus_price
                         }).ToList().Union(
                         (from u in db.dn_discountInfoUsers
                          where u.suit_user_no == userInfo.cardNo
                          && u.dn_discountInfo.res_no == currentResNo
                          && u.dn_discountInfo.end_date > DateTime.Now
                          && u.dn_discountInfo.from_date <= DateTime.Now
                          select new
                          {
                              u.dn_discountInfo.id,
                              u.dn_discountInfo.discount_name,
                              u.dn_discountInfo.discount_rate,
                              u.dn_discountInfo.resume_bigger_than,
                              u.dn_discountInfo.minus_price
                          }).ToList()
                         );

            return Json(infos);
        }

        //获取配送地址，提供autocomplete
        [SessionTimeOutJsonFilter]
        public JsonResult GetDeliveryAddr()
        {
            var addrs = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.is_delivery == true)
                .Select(o => o.delivery_addr).Distinct().ToArray();
            return Json(new { suggestions = addrs });
        }

        //获取订餐电话，提供autocomplete
        [SessionTimeOutJsonFilter]
        public JsonResult GetOrderPhone()
        {
            List<String> phones = new List<string>();
            phones = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.order_phone != null && o.order_phone != "")
                .Select(o => o.order_phone).Distinct().ToList();
            if (!string.IsNullOrEmpty(userInfoDetail.phone) && !phones.Contains(userInfoDetail.phone)) {
                phones.Add(userInfoDetail.phone);
            }
            return Json(new { suggestions = phones.ToArray() });
        }

        //提交申请
        [SessionTimeOutJsonFilter]
        public JsonResult BeginApply(FormCollection fc)
        {
            #region 从前台获取字段
            string mealPlace = fc.Get("mealPlace");
            string payType = fc.Get("payType");
            string peopleNum = fc.Get("peopleNum");
            string deliveryAddr = fc.Get("deliveryAddr");
            string recipient = fc.Get("recipient");
            string recipientPhone = fc.Get("recipientPhone");
            string mealTime = fc.Get("mealTime");
            string orderPhone = fc.Get("orderPhone");
            string comment = fc.Get("comment");
            string discountId = fc.Get("discountId");
            string deskNum = fc.Get("deskNum");
            #endregion

            #region 验证字段
            //就餐时间
            DateTime mealTimeDt;
            string weekDay = "";//周几
            string timeSegment = "";//早中晚餐
            decimal moneySum = 0m; //总价
            int discountIdInt = 0; //优惠ID
            dn_discountInfo discountInfo = null;
            dn_desks desk = null;

            if (!DateTime.TryParse(mealTime, out mealTimeDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "就餐时间格式错误" });
            }
            weekDay = MyUtils.GetWeekDay(mealTimeDt.DayOfWeek);
            try {
                timeSegment = GetTimeSegByOrderTime(mealTimeDt);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            if (string.IsNullOrEmpty(timeSegment)) {
                return Json(new SimpleResultModel() { suc = false, msg = "就餐时间不在营业时间之内,可点击就餐时间输入框右边问号查看" });
            }

            //配送
            if ("配送".Equals(mealPlace)) {
                if (string.IsNullOrEmpty(deliveryAddr)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "请填写配送地点" });
                }
                if (string.IsNullOrEmpty(recipient)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "请填写收件人" });
                }
                if (string.IsNullOrEmpty(recipientPhone)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "请填写联系电话" });
                }
            }

            //逐条对菜式进行验证，验证内容：是否可配送，周几和时间段的限制
            var items = ShoppingCarList().Where(s => s.is_checked == true).ToList();

            foreach (var item in items) {
                if (item.is_selling == false) {
                    return Json(new SimpleResultModel() { suc = false, msg = "该商品已下架：" + item.name });
                }
                if ("配送".Equals(mealPlace)) {
                    if (item.can_delivery == false) {
                        return Json(new SimpleResultModel() { suc = false, msg = "该商品不能配送：" + item.name });
                    }
                }
                if (!"每天".Equals(item.sell_day)) {
                    if (!item.sell_day.Contains(weekDay)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "此商品不能在[ " + weekDay + " ]供应：" + item.name });
                    }
                }
                if (!"全天".Equals(item.sell_time)) {
                    if (!item.sell_time.Contains(timeSegment)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "此商品不能在[ " + timeSegment + " ]供应：" + item.name });
                    }
                }
            }
            moneySum = (decimal)items.Sum(i => i.qty * i.price);

            //订单优惠信息
            discountIdInt = Int32.Parse(discountId);
            if (discountIdInt != 0) {
                try {
                    discountInfo = db.dn_discountInfo.Single(d => d.id == discountIdInt);
                }
                catch {
                    return Json(new SimpleResultModel() { suc = false, msg = "此优惠信息不存在，请刷新页面" });
                }
                if (discountInfo.end_date < DateTime.Now) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此优惠信息已过期" });
                }
                if (discountInfo.for_everyone != true && discountInfo.dn_discountInfoUsers.Where(u => u.suit_user_no == userInfo.cardNo).Count() == 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "对不起，你不享受此优惠" });
                }
                if (discountInfo.discount_name.Contains("折")) {
                    moneySum = Math.Round(moneySum * (decimal)discountInfo.discount_rate) / 10;
                }
                else if (discountInfo.discount_name.Contains("满")) {
                    if (moneySum >= discountInfo.resume_bigger_than) {
                        moneySum -= (decimal)discountInfo.minus_price;
                    }
                }
            }

            //饭卡余额进行验证，余额大于商品总价才可以用饭卡支付            
            if ("饭卡".Equals(payType)) {
                var hasAppliedSum = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.payment_type == "饭卡"
                    && o.cancelled == null && (o.end_flag == 0))
                    .Sum(o => o.real_price != null ? o.real_price : o.total_price);
                if (dinningCarStatusModel.remainingSum < moneySum + (hasAppliedSum ?? 0)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "饭卡余额不足以支付商品总价，请先充值或者选择现金支付" });
                }
            }

            //如果台桌不为空，验证和就餐地点是否吻合            
            if (!string.IsNullOrEmpty(deskNum) && !"配送".Equals(mealPlace)) {
                var desks = db.dn_desks.Where(d => d.number == deskNum && d.belong_to == mealPlace);
                if (desks.Count() == 0) {                    
                    return Json(new SimpleResultModel() { suc = false, msg = mealPlace+"没有这个台桌号，请重新选择" });
                }
                desk=desks.First();
            }

            #endregion

            #region 保存到数据库

            string orderNo;
            try {
                //保存表头
                dn_order order = new dn_order();
                orderNo = GetNextSysNum("DN");
                order.res_no = currentResNo;
                order.order_no = orderNo;
                order.status = "等待审核";
                order.end_flag = 0;
                order.arrive_day = mealTimeDt;
                order.arrive_time = weekDay + timeSegment;
                order.create_time = DateTime.Now;
                order.payment_type = payType;
                order.people_num = Int32.Parse(peopleNum);
                order.total_price = moneySum;
                order.user_comment = comment;
                order.user_name = userInfo.name;
                order.user_no = userInfo.cardNo;
                order.order_phone = orderPhone;
                if ("配送".Equals(mealPlace)) {
                    order.is_delivery = true;
                    order.delivery_addr = deliveryAddr;
                    order.recipient = recipient;
                    order.recipient_phone = recipientPhone;
                }else{
                    if ("包间".Equals(mealPlace)) {
                        order.is_in_room = true;
                    }
                    if (desk != null) {
                        order.desk_num = desk.number;
                        order.desk_name = desk.name;
                    }
                }                 
                if (discountInfo != null) {
                    order.benefit_info = discountInfo.discount_name;
                }
                db.dn_order.Add(order);

                //保存表体并更新购物车状态
                foreach (var item in items) {
                    dn_orderEntry entry = new dn_orderEntry();
                    entry.dn_order = order;
                    entry.disk_id = item.dish_id;
                    entry.price = item.price;
                    entry.qty = item.qty;
                    entry.benefit_info = item.benefit_info;
                    db.dn_orderEntry.Add(entry);

                    //更新购物车
                    dn_shoppingCar car = db.dn_shoppingCar.Single(s => s.id == item.car_id);
                    car.is_submit = true;
                    car.dn_order = order;
                }

                //增加优惠次数
                if (discountInfo != null) {
                    discountInfo.has_benefit_times = discountInfo.has_benefit_times + 1;
                    if (discountInfo.for_everyone != true) {
                        var disUser = discountInfo.dn_discountInfoUsers.Where(u => u.suit_user_no == userInfo.cardNo).First();
                        disUser.has_discount_times = disUser.has_discount_times + 1;
                    }
                }

                db.SaveChanges();
            }
            catch (Exception ex) {
                WriteEventLog("提交申请", "失败：" + ex.Message, -1000);
                return Json(new SimpleResultModel() { suc = false, msg = "服务器错误，请联系管理员" });
            }

            #endregion

            WriteEventLog("提交预约申请", "成功,金额：" + moneySum);
            return Json(new SimpleResultModel() { suc = true, msg = "预约申请提交成功", extra = orderNo });
        }

        //查看我的申请列表
        [SessionTimeOutFilter]
        public ActionResult CheckMyOrders()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }
            WriteEventLog("查看我的订单列表", "打开页面");
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetMyOrders(string fr_date, string to_date, string order_no)
        {
            DateTime frDate, toDate;
            frDate = DateTime.Parse(fr_date);
            toDate = DateTime.Parse(to_date).AddDays(1);
            var result = (from o in db.dn_order
                          where o.user_no == userInfo.cardNo
                          && o.create_time >= frDate
                          && o.create_time <= toDate
                          && o.order_no.Contains(order_no)
                          && o.res_no == currentResNo
                          orderby o.create_time descending
                          select o).ToList();
            var data = (from r in result
                        select new
                        {
                            orderNo = r.order_no,
                            orderDate = ((DateTime)r.create_time).ToString("yyyy-MM-dd HH:mm"),
                            status = r.status,
                            endFlag = r.end_flag,
                            totalPrice = r.real_price == null ? r.total_price : r.real_price,
                            benefitInfo = r.benefit_info,
                            entry = (from oe in r.dn_orderEntry
                                     select new
                                     {
                                         dishId = oe.disk_id,
                                         price = oe.price,
                                         qty = oe.qty,
                                         dishName = oe.dn_dishes.name,
                                         benefitInfo = oe.benefit_info
                                     }).ToList()
                        }).ToList();
            return Json(data);
        }

        //查看申请明细
        [SessionTimeOutFilter]
        public ActionResult CheckOrder(string order_no)
        {
            var orders = db.dn_order.Where(o => o.order_no == order_no && o.user_no == userInfo.cardNo).ToList();
            if (orders.Count() < 1) {
                ViewBag.tip = "单据不存在或无权限查看";
                return View("Error");
            }
            ViewData["order"] = orders.First();
            ViewData["entry"] = orders.First().dn_orderEntry.ToList();
            WriteEventLog("查看单张订单", "订单编号：" + order_no);
            return View();
        }

        //修改支付方式
        [SessionTimeOutJsonFilter]
        public JsonResult ChangePaymentType(string order_no, string payType)
        {
            var orders = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.order_no == order_no).ToList();
            if (orders.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败，订单号不存在" });
            }
            var order = orders.First();
            if (!order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此订单号已被审核或取消，不能修改" });
            }
            order.payment_type = payType;
            db.SaveChanges();
            WriteEventLog("查看详细", "修改支付方式：" + order_no + ":" + payType);
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //取消预约申请
        [SessionTimeOutJsonFilter]
        public JsonResult CancelOrder(string order_no, string reason)
        {
            var orders = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.order_no == order_no).ToList();
            if (orders.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败，订单号不存在" });
            }
            var order = orders.First();
            if (!order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此订单号已被审核或取消，取消失败" });
            }
            order.cancell_reason = reason;
            order.cancelled = true;
            order.status = "用户取消";
            order.end_flag = -1;

            //如果是积分换购的，返还积分
            var pointsOrders = order.dn_orderEntry.Where(o => o.benefit_info != null && o.benefit_info.Contains("积分换购")).ToList();
            if (pointsOrders.Count() > 0) {
                foreach (var po in pointsOrders) {
                    int points = Int32.Parse(po.benefit_info.Split(':')[1]);
                    if (!ReturnPointsIfCanceled(points, "用户取消预约订单")) {
                        return Json(new SimpleResultModel() { suc = false, msg = "取消失败，因为积分返还失败" });
                    }
                }
            }

            //如果是生日餐，要将领取记录作废
            var birthdayOrders = order.dn_orderEntry.Where(o => o.benefit_info != null && o.benefit_info.Contains("生日")).ToList();
            if (birthdayOrders.Count() > 0) {
                foreach (var bo in birthdayOrders) {
                    string thisYear = DateTime.Now.Year.ToString();
                    var meals = db.dn_birthdayMealLog.Where(b => b.user_no == userInfo.cardNo && b.year == thisYear && b.is_cancelled != true);
                    if (meals.Count() > 0) {
                        var meal = meals.First();
                        meal.is_cancelled = true;
                    }
                }
            }

            db.SaveChanges();
            WriteEventLog("查看详细", "取消申请：" + order_no + ":" + reason);
            return Json(new SimpleResultModel() { suc = true, msg = "取消成功" });
        }

        //使用饭卡支付
        [SessionTimeOutJsonFilter]
        public JsonResult PayBillWithDinningCard(string order_no)
        {
            var orders = db.dn_order.Where(o => o.user_no == userInfo.cardNo && o.order_no == order_no).ToList();
            if (orders.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败，订单号不存在" });
            }
            var order = orders.First();
            if (dinningCarStatusModel.remainingSum < (order.real_price ?? order.total_price)) {
                return Json(new SimpleResultModel() { suc = false, msg = "饭卡余额不足以支付订单金额，请先充值或到会所前台使用现金支付" });
            }
            try {
                //使用饭卡支付接口
                if (order.real_price > 0) {

                }
            }
            catch (Exception ex) {
                WriteEventLog("饭卡支付", "支付失败：" + order_no + ";" + ex.Message);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            //增加积分
            int pointMutiple = int.Parse(GetParam("PointsMutipleInt"));  //积分倍数
            if (order.real_price > 0) {
                int toAddedPoint = (int)Math.Floor((decimal)order.real_price) * pointMutiple;
                var points = db.dn_points.Where(p => p.user_no == order.user_no && p.res_no == order.res_no);
                dn_points point = null;
                if (points.Count() > 0) {
                    point = points.First();
                    point.points += toAddedPoint;
                }
                else {
                    point = new dn_points();
                    point.res_no = order.res_no;
                    point.user_no = order.user_no;
                    point.user_name = order.user_name;
                    point.points = toAddedPoint;
                    db.dn_points.Add(point);
                }
                db.dn_pointsRecord.Add(PointRecordConstruct(toAddedPoint, "订单完成送积分（单号：" + order.order_no + "）"));
            }

            order.status = "已完成";
            order.end_flag = 1;
            order.payment_time = DateTime.Now;
            WriteEventLog("饭卡支付", "支付成功：" + order_no);
            return Json(new SimpleResultModel() { suc = true, msg = "支付成功" });
        }

        //查询自己的积分
        public int CheckMyPoints()
        {
            int currentPoint = 0;
            var points = db.dn_points.Where(p => p.user_no == userInfo.cardNo && p.res_no == currentResNo);
            if (points.Count() > 0) {
                currentPoint = points.First().points;
            }

            return currentPoint;
        }

        //查看积分和记录
        [SessionTimeOutFilter]
        public ActionResult CheckPoints()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }
            var records = (from p in db.dn_pointsRecord
                           where p.user_no == userInfo.cardNo
                           && p.res_no == currentResNo
                           orderby p.id descending
                           select p).Take(20).ToList();
            var result = (from r in records
                          select new PointsRecordModel
                          {
                              income = r.income,
                              info = r.info,
                              date = ((DateTime)r.op_date).ToString("yyyy-MM-dd HH:mm")
                          }).ToList();

            ViewData["currentPoint"] = CheckMyPoints();
            ViewData["pointsRecord"] = result;

            WriteEventLog("积分查询", "查询订餐积分与记录：" + CheckMyPoints());

            return View();
        }

        //积分换购
        [SessionTimeOutFilter]
        public ActionResult PointsForDish()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }
            var list = (from p in db.dn_pointsForDish
                        where p.from_date < DateTime.Now
                        && p.end_date > DateTime.Now
                        && p.is_selling == true
                        && p.res_no == currentResNo
                        select p).ToList();
            var result = (from l in list
                          select new PointsForDishModel()
                          {
                              id = l.id,
                              dishId = l.dish_id,
                              dishName = l.dn_dishes.name,
                              pointsNeed = l.points_need,
                              hasFullfill = l.has_fullfill,
                              fromDate = ((DateTime)l.from_date).ToString("yyyy-MM-dd"),
                              endDate = ((DateTime)l.end_date).ToString("yyyy-MM-dd"),
                          }).ToList();
            ViewData["result"] = result;

            WriteEventLog("积分换购", "打开换购界面");
            return View();
        }

        //开始换购
        [SessionTimeOutJsonFilter]
        public JsonResult StartPointsForDish(int id)
        {
            var pfd = db.dn_pointsForDish.Single(p => p.id == id);
            int currentPoint = CheckMyPoints();

            if (pfd.points_need > currentPoint) {
                WriteEventLog("积分换购", "你的积分不足以换购此菜式，剩余积分是：" + currentPoint, -1);
                return Json(new SimpleResultModel() { suc = false, msg = "你的积分不足以换购此菜式，剩余积分是：" + currentPoint });
            }

            //1. 添加进购物车
            dn_shoppingCar car = new dn_shoppingCar();
            car.benefit_info = "积分换购:" + pfd.points_need;
            car.benefit_price = 0;
            car.dish_id = pfd.dish_id;
            car.in_time = DateTime.Now;
            car.is_checked = true;
            car.qty = 1;
            car.user_no = userInfo.cardNo;
            car.res_no = currentResNo;
            db.dn_shoppingCar.Add(car);

            //2. 更新积分
            var points = db.dn_points.Single(p => p.user_no == userInfo.cardNo && p.res_no == currentResNo);
            points.points -= pfd.points_need;

            //3. 添加积分记录
            db.dn_pointsRecord.Add(PointRecordConstruct(-pfd.points_need, "积分换购:" + pfd.dn_dishes.name));

            //4. 增加换购次数
            pfd.has_fullfill += 1;

            db.SaveChanges();

            WriteEventLog("积分换购", pfd.points_need + "分换购" + pfd.dn_dishes.name);
            return Json(new SimpleResultModel() { suc = true, msg = "换购成功，请到购物车查看" });

        }

        //取消换购商品返回积分
        public bool ReturnPointsIfCanceled(int points, string msg)
        {
            try {
                var up = db.dn_points.Single(p => p.user_no == userInfo.cardNo && p.res_no == currentResNo);
                up.points += points;

                //积分记录
                db.dn_pointsRecord.Add(PointRecordConstruct(points, "积分换购返回:" + msg));

                db.SaveChanges();
            }
            catch (Exception ex) {
                WriteEventLog("积分返还", "失败：" + ex.Message, -100);
                return false;
            }

            WriteEventLog("积分返还", "成功返回积分：" + points);
            return true;
        }

        //生日餐
        [SessionTimeOutFilter]
        public ActionResult BirthdayMeal()
        {
            if (string.IsNullOrEmpty(currentResNo)) {
                ViewBag.tip = "请先选择需要进入的食堂";
                return View("Error");
            }
            BirthdayMealModel model = new BirthdayMealModel();
            string birthday = MyUtils.GetBirthdayFromID(userInfoDetail.idNumber);
            string thisYear = DateTime.Now.Year.ToString();
            var birthdayLog = db.dn_birthdayMealLog.Where(b => b.user_no == userInfo.cardNo && b.is_cancelled != true);
            model.hasGotTimes = birthdayLog.Count();
            model.thisYearHasGot = birthdayLog.Where(b => b.year == thisYear).Count() > 0;
            model.birthday = birthday;
            model.nowCanGetMeal = !model.thisYearHasGot && DateTime.Now.Month == (Int32.Parse(birthday.Substring(0, 2)));

            model.dishList = (from d in db.dn_dishes
                              where d.is_birthday_meal == true
                              && d.is_selling == true
                              && d.res_no == "HS"  //只有会所才有生日餐
                              select new birthDishModel()
                              {
                                  dishId = d.id,
                                  dishName = d.name,
                                  hasGotNum = db.dn_birthdayMealLog.Where(b => b.is_cancelled != true && b.dish_name == d.name).Count()
                              }).ToList();
            WriteEventLog("会所生日套餐", "查看套餐列表");

            ViewData["model"] = model;
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetBirthdayMeal(int dishId)
        {
            string thisYear = DateTime.Now.Year.ToString();
            var thisYearHasGot = db.dn_birthdayMealLog.Where(b => b.user_no == userInfo.cardNo && b.is_cancelled != true && b.year == thisYear).Count() > 0;
            if (thisYearHasGot) {
                return Json(new SimpleResultModel() { suc = false, msg = "生日套餐今年已领取，请明年再来" });
            }
            string birthday = MyUtils.GetBirthdayFromID(userInfoDetail.idNumber);

            if (DateTime.Now.Month != (Int32.Parse(birthday.Substring(5, 2)))) {
                return Json(new SimpleResultModel() { suc = false, msg = "只有生日当月才可以领取" });
            }

            try {
                //1. 放到购物车
                var car = new dn_shoppingCar();
                car.dish_id = dishId;
                car.benefit_info = "生日套餐优惠";
                car.benefit_price = 0;
                car.in_time = DateTime.Now;
                car.is_checked = true;
                car.qty = 1;
                car.user_no = userInfo.cardNo;
                car.res_no = currentResNo;
                db.dn_shoppingCar.Add(car);

                //2. 添加领取记录
                var log = new dn_birthdayMealLog();
                log.user_no = userInfo.cardNo;
                log.year = DateTime.Now.Year.ToString();
                log.op_date = DateTime.Now;
                log.is_cancelled = false;
                log.birthday = birthday;
                log.dish_name = db.dn_dishes.Single(d => d.id == dishId).name;
                db.dn_birthdayMealLog.Add(log);

                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "领取失败，原因：" + ex.Message });
            }

            WriteEventLog("领取生日餐", "领取成功：" + dishId);
            return Json(new SimpleResultModel() { suc = true, msg = "领取成功，请到购物车查看" });
        }

        /// <summary>
        /// 快速构造一个积分记录的对象
        /// </summary>
        /// <param name="income">积分变化</param>
        /// <param name="comment">备注</param>
        /// <returns>积分记录对象</returns>
        private dn_pointsRecord PointRecordConstruct(int income, string comment)
        {
            dn_pointsRecord record = new dn_pointsRecord();
            record.income = income;
            record.info = comment;
            record.op_date = DateTime.Now;
            record.user_no = userInfo.cardNo;
            record.user_name = userInfo.name;
            record.res_no = currentResNo;
            return record;
        }

        //食堂营业时间
        [SessionTimeOutJsonFilter]
        public JsonResult GetServiceTimeSeg()
        {
            var segs = (from i in db.dn_items
                        where i.comment.EndsWith("时间段")
                        && i.res_no == currentResNo
                        select new
                        {
                            segValue = i.value,
                            segName = i.comment.Substring(0, 2)
                        }).ToList();
            return Json(segs);
        }

        //加载所有台桌和已占用数量
        [SessionTimeOutFilter]
        public ActionResult VisibleDesks(string orderDate, string mealPlace)
        {
            DateTime orderDt;
            if (!DateTime.TryParse(orderDate, out orderDt)) {
                ViewBag.tip = "就餐时间不合法";
                return View("Error");
            }
            string weekday = MyUtils.GetWeekDay(orderDt.DayOfWeek);
            string timeSeg = GetTimeSegByOrderTime(orderDt);
            if (string.IsNullOrEmpty(timeSeg)) {
                ViewBag.tip = "当前时间段不在营业时间内";
                return View("Error");
            }


            //获取预约时间间隔
            int intervalMinutes = 0;
            if (mealPlace.Equals("大堂")) {
                intervalMinutes = int.Parse(GetParam("IntervalMinutesHallInt"));
            }
            else if (mealPlace.Equals("包间")) {
                intervalMinutes = int.Parse(GetParam("IntervalMinutesRoomInt"));
            }

            DateTime minTime = orderDt.AddMinutes(-intervalMinutes);
            DateTime maxTime = orderDt.AddMinutes(intervalMinutes);
            var allDesks = (from d in db.dn_desks
                            where d.belong_to == mealPlace
                            select new VisualDeskModel()
                            {
                                number = d.number,
                                name = d.name,
                                belongTo = d.belong_to,
                                isCancel = d.is_cancel,
                                seatQty = d.seat_qty,
                                zone = d.number.Substring(0, 1),
                                seatHasTaken = (from o in db.dn_order
                                                where o.end_flag == 0
                                                && o.desk_num == d.number
                                                && o.arrive_day >= minTime
                                                && o.arrive_day <= maxTime
                                                select o.people_num).Sum(),
                                nowCanUse = (d.open_weekday == "每天" || d.open_weekday.Contains(weekday))
                                            && (d.open_time == "全天" || d.open_time.Contains(timeSeg))
                            }).ToList();
            var deskInfo = (from d in allDesks
                            orderby d.zone
                            group d by new
                            {
                                d.belongTo,
                                d.zone
                            } into dk
                            select new DeskInfoModel()
                           {
                               belongTo = dk.Key.belongTo,
                               zone = dk.Key.zone,
                               maxRow = dk.Max(k => int.Parse(k.number.Substring(1, k.number.IndexOf('-') - 1))), //最大行
                               maxCol = dk.Max(k => int.Parse(k.number.Substring(k.number.IndexOf('-') + 1))) //最大列
                           }).ToList();
            //allDesks.Skip(20).Take(10).ToList().ForEach(a => a.seatHasTaken = 3);
            //allDesks.Skip(40).Take(10).ToList().ForEach(a => a.seatHasTaken = 6);
            ViewData["desks"] = allDesks;
            ViewData["deskInfo"] = deskInfo;
            return View();
        }

        //批量增加台桌
        //public string AddDesks()
        //{
        //    for (var i = 1; i <= 10; i++) {
        //        for (var j = 1; j <= 6; j++) {
        //            db.dn_desks.Add(new dn_desks()
        //            {
        //                number = string.Format("A{0}-{1}", i, j),
        //                name = string.Format("A区{0}排{1}位", i, j),
        //                belong_to = "大堂",
        //                create_time = DateTime.Now,
        //                create_user = 1,
        //                is_cancel = false,
        //                last_update_time = DateTime.Now,
        //                open_time = "全天",
        //                open_weekday = "每天",
        //                seat_qty = 4
        //            });
        //        }
        //    }
        //    db.SaveChanges();
        //    return "suc";
        //}

    }
}
