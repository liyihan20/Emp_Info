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
    
    public partial class ei_spApply
    {
        public ei_spApply()
        {
            this.ei_spApplyEntry = new HashSet<ei_spApplyEntry>();
        }
    
        public int id { get; set; }
        public string sys_no { get; set; }
        public string applier_num { get; set; }
        public string applier_name { get; set; }
        public string applier_phone { get; set; }
        public Nullable<System.DateTime> apply_time { get; set; }
        public string send_or_receive { get; set; }
        public string company { get; set; }
        public string bus_name { get; set; }
        public string send_no { get; set; }
        public string content_type { get; set; }
        public Nullable<int> package_num { get; set; }
        public Nullable<decimal> total_weight { get; set; }
        public Nullable<int> cardboard_num { get; set; }
        public string cardboard_size { get; set; }
        public string box_size { get; set; }
        public string aging { get; set; }
        public string from_addr { get; set; }
        public string to_addr { get; set; }
        public string receiver_name { get; set; }
        public string receiver_phone { get; set; }
        public string ex_company { get; set; }
        public string ex_type { get; set; }
        public Nullable<decimal> ex_price { get; set; }
        public string ex_no { get; set; }
        public string apply_reason { get; set; }
        public string ex_log { get; set; }
        public Nullable<bool> has_attachment { get; set; }
        public Nullable<System.DateTime> out_time { get; set; }
        public string out_guard { get; set; }
        public Nullable<bool> can_print { get; set; }
        public string scope { get; set; }
        public string out_status { get; set; }
        public string out_reason { get; set; }
        public string isReturnBack { get; set; }
    
        public virtual ICollection<ei_spApplyEntry> ei_spApplyEntry { get; set; }
    }
}
