using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EmpInfo.Models;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmpInfo.Util
{
    public class MyUtils
    {
        //生成随机数列
        public static string CreateValidateNumber(int length)
        {
            //去掉数字0和字母o，因为不容易区分
            string Vchar = "1,2,3,4,5,6,7,8,9,a,b,c,d,e,f,g,h,i,j,k,l,m,n,p" +
            ",q,r,s,t,u,v,w,x,y,z,A,B,C,D,E,F,G,H,I,J,K,L,M,N,P,Q" +
            ",R,S,T,U,V,W,X,Y,Z";

            string[] VcArray = Vchar.Split(new Char[] { ',' });//拆分成数组
            string num = "";

            int temp = -1;//记录上次随机数值，尽量避避免生产几个一样的随机数

            Random rand = new Random();
            //采用一个简单的算法以保证生成随机数的不同
            for (int i = 1; i < length + 1; i++)
            {
                if (temp != -1)
                {
                    rand = new Random(i * temp * unchecked((int)DateTime.Now.Ticks));
                }

                int t = rand.Next(VcArray.Length - 1);
                if (temp != -1 && temp == t)
                {
                    return CreateValidateNumber(length);

                }
                temp = t;
                num += VcArray[t];
            }
            return num;
        }

        public static byte[] CreateValidateGraphic(string validateCode)
        {
            Bitmap image = new Bitmap((int)Math.Ceiling(validateCode.Length * 18.0), 26);
            Graphics g = Graphics.FromImage(image);
            try
            {
                //生成随机生成器
                Random random = new Random();
                //清空图片背景色
                g.Clear(Color.White);
                //画图片的干扰线
                for (int i = 0; i < 25; i++)
                {
                    int x1 = random.Next(image.Width);
                    int x2 = random.Next(image.Width);
                    int y1 = random.Next(image.Height);
                    int y2 = random.Next(image.Height);
                    g.DrawLine(new Pen(Color.Silver), x1, y1, x2, y2);
                }
                Font font = new Font("Arial", 16, (FontStyle.Bold | FontStyle.Italic));
                LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, image.Width, image.Height),
                 Color.Blue, Color.DarkRed, 1.2f, true);
                g.DrawString(validateCode, font, brush, 3, 2);
                //画图片的前景干扰点
                for (int i = 0; i < 100; i++)
                {
                    int x = random.Next(image.Width);
                    int y = random.Next(image.Height);
                    image.SetPixel(x, y, Color.FromArgb(random.Next()));
                }
                //画图片的边框线
                g.DrawRectangle(new Pen(Color.Silver), 0, 0, image.Width - 1, image.Height - 1);
                //保存图片数据
                MemoryStream stream = new MemoryStream();
                image.Save(stream, ImageFormat.Jpeg);
                //输出图片流
                return stream.ToArray();
            }
            finally
            {
                g.Dispose();
                image.Dispose();
            }
        }

        public static string getMD5(string str)
        {
            if (str.Length > 2)
            {
                str = "Who" + str.Substring(2) + "Are" + str.Substring(0, 2) + "You";
            }
            else
            {
                str = "Who" + str + "Are" + str + "You";
            }
            return getNormalMD5(str);

        }

        public static string getNormalMD5(string str)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] data = Encoding.Default.GetBytes(str);
            byte[] result = md5.ComputeHash(data);
            String ret = "";
            for (int i = 0; i < result.Length; i++) {
                ret += result[i].ToString("x").PadLeft(2, '0');
            }
            return ret;
        }

        //将中文编码为utf-8
        public static string EncodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlEncode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        //将utf-8解码
        public static string DecodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlDecode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        //权限判断
        public static bool hasGotPower(int userId, string controlerName, string actionName)
        {
            ICAuditEntities db = new ICAuditEntities();
            try
            {
                var power = from a in db.ei_authority
                            from u in db.ei_groups
                            from ga in a.ei_groupAuthority
                            from gu in u.ei_groupUser
                            where ga.group_id == u.id
                            && gu.user_id == userId
                            && a.controler_name == controlerName
                            && a.action_name == actionName
                            select a;
                if (power.Count() > 0)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }

        }

        //周几的转换
        public static string GetWeekDay(DayOfWeek dw)
        {
            string result = ""; ;
            switch (dw) {
                case DayOfWeek.Monday:
                    result = WeekDay.周一.ToString();
                    break;
                case DayOfWeek.Tuesday:
                    result = WeekDay.周二.ToString();
                    break;
                case DayOfWeek.Wednesday:
                    result = WeekDay.周三.ToString();
                    break;
                case DayOfWeek.Thursday:
                    result = WeekDay.周四.ToString();
                    break;
                case DayOfWeek.Friday:
                    result = WeekDay.周五.ToString();
                    break;
                case DayOfWeek.Saturday:
                    result = WeekDay.周六.ToString();
                    break;
                case DayOfWeek.Sunday:
                    result = WeekDay.周日.ToString();
                    break;
            }
            return result;
        }

        //早中晚的时间段转化 时间段作为后台参数设置，是变动的，已弃用，改用base controller 同名方法 2016-9-23
        public static string GetTimeSegment(int hour, int minutes = 0)
        {
            string result = "";
            if (hour >= 7 && hour <= 9) {
                result = MealSegment.早餐.ToString();
            }
            else if (hour >= 10 && hour <= 15) {
                result = MealSegment.午餐.ToString();
            }
            else if (hour >= 16 && hour <= 20) {
                result = MealSegment.晚餐.ToString();
            }
            else if (hour >= 21 && hour <= 23) {
                result = MealSegment.宵夜.ToString();
            }

            return result;
        }

        //生成订单编号,因生成的字符串含有特殊字符，已弃用
        public static string GetOrderNo(string prefix = "DN")
        {
            return prefix + DateTime.Now.GetHashCode();
        }

        //从身份证编号读取出生日
        public static string GetBirthdayFromID(string idNumber)
        {
            //从第7位开始截取8位生日，只截取月和日
            //return idNumber.Substring(6, 4) + "-" + idNumber.Substring(10, 2) + "-" + idNumber.Substring(12, 2);
            return idNumber.Substring(10, 2) + "-" + idNumber.Substring(12, 2);
        }

        public static byte[] GetServerImage(string url)
        {
            Image im = Image.FromFile(url);
            using (MemoryStream ms = new MemoryStream()) {
                im.Save(ms, ImageFormat.Png);
                byte[] portrait = new byte[ms.Length];
                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(portrait, 0, portrait.Length);
                return portrait;
            }
        }

        //验证登录密码，2013-5-27后开启复杂性密码，强制用户修改。
        public static string ValidatePassword(string str)
        {
            string alph = @"[A-Za-z]+";
            string num = @"\d+";
            //string cha = @"[\-`=\\\[\];',\./~!@#\$%\^&\*\(\)_\+\|\{\}:""<>\?]+";
            if (!new Regex(alph).IsMatch(str)) {
                return "新密码必须包含英文字母，保存失败。英文字母有：A~Z，a~z";
            }
            if (!new Regex(num).IsMatch(str)) {
                return "新密码必须包含阿拉伯数字，保存失败。数字有：0~9";
            }
            //if (!new Regex(cha).IsMatch(str)) {
            //    return @"新密码必须包含特殊字符，保存失败。特殊字符有：-`=\[];',./~!@#$%^&*()_+|{}:""<>?";
            //}
            return "";
        }

        //将保存在数据库的图片二进制转化为Image格式
        public static Image BytesToImage(byte[] buffer)
        {
            using (MemoryStream ms = new MemoryStream(buffer)) {
                Image image = System.Drawing.Image.FromStream(ms);
                return image;
            }
        }

        //生成缩略图
        public static byte[] MakeThumbnail(Image originalImage, int width = 0, int height = 128, string mode = "H")
        {
            int towidth = width;
            int toheight = height;

            int x = 0;
            int y = 0;
            int ow = originalImage.Width;
            int oh = originalImage.Height;

            switch (mode) {
                case "HW"://指定高宽缩放（可能变形）                 
                    break;
                case "W"://指定宽，高按比例                     
                    toheight = originalImage.Height * width / originalImage.Width;
                    break;
                case "H"://指定高，宽按比例 
                    towidth = originalImage.Width * height / originalImage.Height;
                    break;
                case "Cut"://指定高宽裁减（不变形）                 
                    if ((double)originalImage.Width / (double)originalImage.Height > (double)towidth / (double)toheight) {
                        oh = originalImage.Height;
                        ow = originalImage.Height * towidth / toheight;
                        y = 0;
                        x = (originalImage.Width - ow) / 2;
                    }
                    else {
                        ow = originalImage.Width;
                        oh = originalImage.Width * height / towidth;
                        x = 0;
                        y = (originalImage.Height - oh) / 2;
                    }
                    break;
                default:
                    break;
            }

            //新建一个bmp图片 
            Image bitmap = new System.Drawing.Bitmap(towidth, toheight);

            //新建一个画板 
            Graphics g = System.Drawing.Graphics.FromImage(bitmap);

            //设置高质量插值法 
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;

            //设置高质量,低速度呈现平滑程度 
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            //清空画布并以透明背景色填充 
            g.Clear(Color.Transparent);

            //在指定位置并且按指定大小绘制原图片的指定部分 
            g.DrawImage(originalImage, new Rectangle(0, 0, towidth, toheight),
                new Rectangle(x, y, ow, oh),
                GraphicsUnit.Pixel);

            try {
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream()) {
                    bitmap.Save(ms, originalImage.RawFormat);
                    bytes = ms.ToArray();
                }
                return bytes;
            }
            catch (System.Exception e) {
                throw e;
            }
            finally {
                originalImage.Dispose();
                g.Dispose();
            }
        }

    }
}