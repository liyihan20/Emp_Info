//2021-11-23 改为post异步调用，并省略掉前台的ready钩子,url参数不能使用encodeURIComponent进行转义！否则会提示签名错误，找了很久才找到。用$.ajax()里面的data传参成功是因为会自动decode掉
function qywxJs(opt) {
    if (!opt) opt = {};
    $.post("http://emp.truly.com.cn/Emp/QYWX/GetConfigParam", { url: window.location.href.split("#")[0] }, function (result) {
        if (!result.suc) {
            alert("服务器错误");
            return;
        }
        var p = JSON.parse(result.param);
        //配置参数
        wx.config({
            beta: p.beta, // 必须这么写，否则wx.invoke调用形式的jsapi会有问题
            debug: opt.debug || false, // 开启调试模式,调用的所有api的返回值会在客户端alert出来，若要查看传入的参数，可以在pc端打开，参数信息会通过log打出，仅在pc端时才会打印。
            appId: p.appId, // 必填，企业微信的corpID
            timestamp: p.timestamp, // 必填，生成签名的时间戳
            nonceStr: p.nonceStr, // 必填，生成签名的随机串
            signature: p.signature,// 必填，签名
            jsApiList: ['scanQRCode'] // 必填，需要使用的JS接口列表:'getLocation','scanQRCode'
        });
    });

    var ScanCode = function (callback) {
        wx.ready(function () {
            wx.scanQRCode({
                needResult: 1, // 默认为0，扫描结果由微信处理，1则直接返回扫描结果，
                scanType: ["qrCode", "barCode"], // 可以指定扫二维码还是一维码，默认二者都有
                success: function (res) {
                    callback(res.resultStr);
                },
                error: function (res) {
                    callback(res.errMsg);
                }
            });
        });
    }

    return { ScanCode: ScanCode };

}