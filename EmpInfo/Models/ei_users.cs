//------------------------------------------------------------------------------
// <auto-generated>
//    此代码是根据模板生成的。
//
//    手动更改此文件可能会导致应用程序中发生异常行为。
//    如果重新生成代码，则将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace EmpInfo.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class ei_users
    {
        public ei_users()
        {
            this.ei_groupUser = new HashSet<ei_groupUser>();
            this.ei_deliveryInfo = new HashSet<ei_deliveryInfo>();
        }
    
        public int id { get; set; }
        public string card_number { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public Nullable<bool> forbit_flag { get; set; }
        public Nullable<System.DateTime> create_date { get; set; }
        public Nullable<System.DateTime> last_login_date { get; set; }
        public string password { get; set; }
        public Nullable<int> fail_times { get; set; }
        public string sex { get; set; }
        public string id_number { get; set; }
        public string salary_no { get; set; }
        public string short_phone { get; set; }
        public byte[] short_portrait { get; set; }
    
        public virtual ICollection<ei_groupUser> ei_groupUser { get; set; }
        public virtual ICollection<ei_deliveryInfo> ei_deliveryInfo { get; set; }
    }
}
