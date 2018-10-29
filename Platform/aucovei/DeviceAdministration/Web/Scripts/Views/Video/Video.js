$(document).ready(function () {

    var webSocketVideoFrame;
    var frameTime;
    var lastImageUrl;

    function GetVideoFrames() {

        webSocketVideoFrame =
            new WebSocket("wss://aucoveidemo.azurewebsites.net/api/v1/videoframes/receiver?deviceid=vivek");
        webSocketVideoFrame.binaryType = "arraybuffer";

        webSocketVideoFrame.onopen = function () {

        };

        webSocketVideoFrame.onmessage = function () {
            var bytearray = new Uint8Array(event.data);
            var blob = new Blob([event.data], { type: "image/jpeg" });
            lastImageUrl = createObjectURL(blob);

            $("#videoFrame").on("load", function () {
                $('#videoFrame').show();
                $('#videoloading').hide();
                URL.revokeObjectURL(lastImageUrl);
            }).attr("src", lastImageUrl);

            frameTime = new Date().getTime();
        };
    }

    function createObjectURL(blob) {
        var URL = window.URL || window.webkitURL;
        if (URL && URL.createObjectURL) {
            return URL.createObjectURL(blob);
        } else {
            return null;
        }
    }

    function KeepAliveGetVideoFrames() {
        var duration = 0;
        if (frameTime !== undefined) {
            duration = new Date().getTime() - frameTime;
        }

        if (frameTime !== undefined
            && duration <= 1000) {

            setTimeout(function () {
                KeepAliveGetVideoFrames();
            }, 100);
        } else {
            if (webSocketVideoFrame !== undefined) {
                try {
                    webSocketVideoFrame.close();
                } catch (e) {
                    // do nothing
                }
            }

            GetVideoFrames();

            setTimeout(function () {
                KeepAliveGetVideoFrames();
            }, 4000);
        }
    }

    KeepAliveGetVideoFrames();
});