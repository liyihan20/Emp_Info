var utils = {
    //将对象的属性值插入表单的同名元素中
    loadObjData: function ($fm, obj) {
        var key, value, tagName, type, arr;
        for (x in obj) {
            key = x;
            value = obj[x];
            $fm.find("[name='" + key + "'],[name='" + key + "[]']").each(function () {
                tagName = $(this)[0].tagName;
                type = $(this).attr('type');
                if (tagName == 'INPUT') {
                    if (type == 'radio') {
                        $(this).attr('checked', $(this).val() == value);
                    } else if (type == 'checkbox') {
                        arr = value.split(',');
                        for (var i = 0; i < arr.length; i++) {
                            if ($(this).val() == arr[i]) {
                                $(this).attr('checked', true);
                                break;
                            }
                        }
                    } else {
                        $(this).val(value);
                    }
                } else if (tagName == 'SELECT' || tagName == 'TEXTAREA') {
                    $(this).val(value);
                }

            });
        }
    },
    //将表单元素的值提取放入对象中
    getFormObj: function ($form) {
        var array = $form.serializeArray();
        var obj = {};
        for (var i = 0; i < array.length; i++) {
            obj[array[i].name] = $.trim(array[i].value); //将值设置到对象，顺便去掉前后空格
        }
        return obj;
    },
    //将数组元素去重后返回
    unique: function (arr) {
        var formArr = arr.sort();
        var newArr = [formArr[0]];
        for (var i = 1; i < formArr.length; i++) {
            if (formArr[i] !== formArr[i - 1]) {
                newArr.push(formArr[i]);
            }
        }
        return newArr;
    },
    //将后台到前台的日期格式化为年-月-日的格式，第二个参数指定是否包含小时和分钟
    parseTDate: function (d, hasHour) {
        if (!d) return "";
        if (d.indexOf("T") > 0) {
            return d.split("T")[0];
        } else if (d.indexOf("Date") >= 0) {
            var date = eval('new ' + eval(d).source)
            var date_str = date.getFullYear() + '-' + (date.getMonth() + 1) + '-' + date.getDate() + " ";
            if (hasHour) {
                date_str += (date.getHours() < 10 ? '0' + date.getHours() : date.getHours()) + ":" + (date.getMinutes() < 10 ? '0' + date.getMinutes() : date.getMinutes());
            }
            return date_str;
        } else {
            return d;
        }
    },
    //测试是否尺寸，必须符合 数字*数字*数字 的格式，数字可包含小数
    testIsSize: function (str) {
        var reg = new RegExp(/^[0-9]+(\.{1}[0-9]+){0,1}(\*[0-9]+(\.{1}[0-9]+){0,1}){2}$/);
        return reg.test(str);
    },
    //测试是否整数
    testIsInt: function (str) {
        var reg = new RegExp(/^[0-9]+$/);
        return reg.test(str);
    },
    //测试是否符合指定小数位的数字，如果指定小数位是3，那给出的必须是小于3位的小数或整数；如果不指定小数位，则最多可以包含100个小数位，相当于没限制
    testIsFloat: function (str, digitPoint) {
        if (!digitPoint || isNaN(digitPoint)) digitPoint = 100;
        var reg = new RegExp("^[0-9]+(\.[0-9]{1," + digitPoint + "}){0,1}$");
        return reg.test(str);
    },
    //重置form表单，reset方法默认不能重置hidden的值，此方法完善此功能,需要配合data-value属性，里面保存默认值
    resetForm: function ($fm) {
        $fm[0].reset();
        $fm.find("input[type='hidden']").each(function (v) {
            $(this).val($(this).attr("data-value"));
        });
    },
    //验证表单中的必填项
    validateRequiredField: function ($fm) {
        //1. input里面的required属性
        var suc = true;
        var msg = "";
        $fm.find("input:required").each(function (i, v) {
            if (!v.value || $.trim(v.value) == "") {
                console.log(v.name);
                msg = "【" + utils.getLabelName($fm, v.name) + "】必须填写";
                suc = false;
                return false;
            }
        });
        if (!suc) return { suc: suc, msg: msg };

        //2. select里面的required属性
        $fm.find("select:required").each(function (i, v) {
            if (!v.value || $.trim(v.value) == "") {
                msg = "【" + utils.getLabelName($fm, v.name) + "】必须选择";
                suc = false;
                return false;
            }
        });
        if (!suc) return { suc: suc, msg: msg };

        //3. textarea里面的required属性
        $fm.find("textarea:required").each(function (i, v) {
            if (!v.value || $.trim(v.value) == "") {
                console.log(v.name);
                msg = "【" + utils.getLabelName($fm, v.name) + "】必须填写";
                suc = false;
                return false;
            }
        });
        if (!suc) return { suc: suc, msg: msg };

        return { suc: true };
    },
    //获取表单控件对应的标签。必须是b-label和b-input的格式
    getLabelName: function ($fm, controlName) {
        var labelName = $.trim($fm.find("[name=" + controlName + "]").parent(".b-input").prev(".b-label").html());
        if (labelName == "") {
            //兼容input-group-addon的格式
            labelName = $.trim($fm.find("[name=" + controlName + "]").prev(".input-group-addon").html());
        }
        return labelName;
    },
    //计算数字数组的和，或者对象数组中某一个字段的和
    CalArrSum: function (arr, field) {
        var sum = 0;
        if (arr.length < 1) return 0;
        if (typeof (arr[0]) == "number") {
            for (var i in arr) {
                sum += parseFloat(arr[i]);
            }
        } else if (typeof (arr[0] == "object")) {
            for (var i in arr) {
                if (!isNaN(arr[i][field])) {
                    sum += parseFloat(arr[i][field]);
                }
            }
        }
        return sum;
    },
    //节流，将高频率的相同操作当做一个操作，delay为毫秒数
    Debounce: function(fn, delay) {
        var timer = null;

        return function () {
            var args = arguments;
            var context = this;

            if (timer) {
                clearTimeout(timer);
                timer = setTimeout(function () {
                    fn.apply(context, args);
                }, delay);
            } else {
                timer = setTimeout(function () {
                    fn.apply(context, args);
                }, delay);
            }
        }
    },
    //获取下载路径
    GetDownloadRoute: function (sysNum) {
        return "../Att/" + sysNum.substr(0, 2) + "/20" + sysNum.substr(2, 2) + "/" + sysNum.substr(4, 2) + "/" + sysNum + "/";
    }
}
