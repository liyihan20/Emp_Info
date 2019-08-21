var utils = {
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
    getFormObj: function ($form) {
        var array = $form.serializeArray();
        var obj = {};
        for (var i = 0; i < array.length; i++) {
            obj[array[i].name] = array[i].value;
        }
        return obj;
    },
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
    parseTDate: function (d, hasHour) {
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
    }
}  
