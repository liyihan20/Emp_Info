(function ($, window) {

    // 初始化插件参数并配置插件各项事件
    function init(item, options) {
        var defaults = {
            sysNum: "",
            prefix: ""
        };
        var opts = $.extend(defaults, options);

        if (opts.sysNum == "") {
            console.error("流水号不能为空");
            return;
        }

        var target = $(item);
                
        var html = '<ul class="list-group"></ul>';
        target.append(html);

        var ulList = target.find("ul");

        $.post("../Item/GetAttachments", { sysNo: opts.sysNum }, function (data) {
            if (data.length > 0) {
                for (var i = 0; i < data.length; i++) {
                    var shortName = data[i].fileName;
                    if (opts.prefix) {
                        if (shortName.indexOf(opts.prefix) != 0) {
                            continue;
                        }
                    }
                    if (shortName.length > 25) {
                        var ext = shortName.substr(shortName.lastIndexOf("."));
                        shortName = shortName.substr(0, 20) + ".." + ext;
                    }
                    $(ulList).append('<li class="list-group-item">\
                                    <a href="#" data-fd="' + data[i].fileName + '"><i class="fa fa-download"></i> ' + shortName + '(' + (data[i].fileSize > 1000000 ? ((data[i].fileSize / 1000000).toFixed(1) + 'M') : ((data[i].fileSize / 1000).toFixed(1) + 'K')) + ')</a>\
                                    </li>');
                }
            }
        });

        $(ulList).on("click", "a", function () {
            var fileName = $(this).attr("data-fd");
            var url = utils.GetDownloadRoute(opts.sysNum) + fileName;
            window.open(url);
        });

    }

    //对外主接口
    $.fn.myDownloader = function (options) {
        var ele = this;
        init(ele, options);
    }   

})(jQuery, window);