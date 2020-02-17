(function ($, window) {

    // 初始化插件参数并配置插件各项事件
    function init(item,sysNum, options) {
        var defaults = {
            swf: '../Content/webupload/Uploader.swf',
            server: '../File/BeginUpload',            
            resize: false,
            auto: true,
            pick: "#picker",
            formData: { sysNum: sysNum },
            fileNumLimit: 3,
            fileSingleSizeLimit: 10 * 1024 * 1024,
            accept: {
                title: 'Images',
                extensions: 'gif,jpg,jpeg,bmp,png',
                mimeTypes: 'image/*'
            }
        };
        var opts = $.extend(defaults, options);
        var target = $(item);
        
        var uploader = WebUploader.create(defaults);
        var html = '<div class="panel panel-default" id="al_file_panel" style="display:none;">\
                    <div class="panel-heading">附件信息</div>\
                    <div class="panel-body" style="padding:0 8px;">\
                        <table class="table table-condensed table-hover" id="al_file_tb" style="margin:10px 0;">\
                            <thead>\
                                <tr>\
                                    <th style="width:50%;">名称</th>\
                                    <th style="width:20%;">大小</th>\
                                    <th style="width:30%;">状态</th>\
                                </tr>\
                            </thead>\
                            <tbody id="al_file_list"></tbody>\
                        </table>\
                    </div>\
                </div>';
        target.append(html);
        var filePanelEle = target.find("#al_file_panel");
        var fileListEle = target.find("#al_file_list");

        uploader.on('fileQueued', function (file) {
            file.name = file.name.replace(/&/g, "_");
            var fileName = file.name;
            if (fileName.length > 20) {
                var ext = fileName.substr(fileName.lastIndexOf("."));
                fileName = fileName.substr(0, 14) + ".." + ext;
            }
            $(fileListEle).append('<tr><td id="' + file.id + '" data-fd="' + file.name + '" class="item">' +
                fileName +
            '</td><td>' + (file.size / 1000).toFixed(1) + " K</td><td class='state'>上传中...</td></tr>");
            $(filePanelEle).show();
        });

        uploader.on('uploadSuccess', function (file, res) {
            if (!res.suc) {
                $('#' + file.id).parent().find('td.state').addClass("text-danger").html("上传出错");
                console.log(res);
            } else {
                $('#' + file.id).parent().find('td.state').addClass("text-success").html("已上传| <i class='fa fa-times-circle-o text-danger' title='移除'></i>");
            }
        });

        uploader.on('uploadError', function (file) {
            $('#' + file.id).parent().find('td.state').addClass("text-danger").html('上传出错');
        });

        uploader.on("error", function (type) {
            switch (type) {
                case "Q_TYPE_DENIED":
                    toastr.error("文件格式不正确");
                    break;
                case "F_EXCEED_SIZE":
                    toastr.error("单个文件大小必须少于" + (defaults.fileSingleSizeLimit / 1024 / 1024) + "M");
                    break;
                case "F_DUPLICATE":
                    toastr.error("不能重复上传文件");
                    break;
                case "Q_EXCEED_NUM_LIMIT":
                    toastr.error("最多上传文件数量是" + defaults.fileNumLimit + "个");
                    break;
                default:
                    console.error("上传失败：" + type);
                    break;
            }
        });

        $(fileListEle).on("click", "td i", function () {
            var fileTD = $(this).parents("tr").find("td")[0];
            openConfirmDialog("确定要移除此上传的文件吗？", function () {
                var fileId = $(fileTD).attr("id");
                var fileName = $(fileTD).attr("data-fd");

                $.post("../File/RemoveUploadedFile", { sysNum: sysNum, fileName: fileName }, function (data) {
                    if (data.suc) {
                        toastr.success("文件移除成功");
                        uploader.removeFile(fileId, true);
                        $(fileTD).parent().fadeOut(1000, function () {
                            $(fileTD).parent().remove();
                            var st = uploader.getStats();
                            if (st.successNum == st.cancelNum) {
                                $(filePanelEle).fadeOut(1000);
                            }
                        });
                    } else {
                        toastr.error("移除失败：" + data.msg);
                    }
                });
            });
        });
    }

    //对外主接口
    $.fn.myUploader = function (options) {
        var ele = this;
        init(ele, options.sysNum, options);
    }

    //上传插件数量
    $.fn.getMyUploaderFilesNum = function (options) {
        var ele = this;
        return $(ele).find("#al_file_list").children().length;
    }

})(jQuery, window);