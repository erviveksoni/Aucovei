IoTApp.createModule('IoTApp.DeviceDetails', function () {
    "use strict";

    $.ajaxSetup({ cache: false });
    var self = this;
    var loadDataUrlBase;
    var refreshMilliseconds;
    var telemetryDataUrl;
    var timerId;
    var webSocketVideoFrame;
    var frameTime;
    var lastImageUrl;
    var wstimer;
    var deviceId;

    var getDeviceDetailsView = function (deviceId) {
        $('#loadingElement').show();

        self.deviceId = deviceId;
        telemetryDataUrl =
            loadDataUrlBase + encodeURIComponent(deviceId);

        if (timerId) {
            clearTimeout(timerId);
            timerId = null;
        }

        $.get('/Device/GetDeviceDetails', { deviceId: deviceId }, function (response) {
            if (!$(".details_grid").is(':visible')) {
                IoTApp.DeviceIndex.toggleDetails();
            }
            onDeviceDetailsDone(response);
            refreshData();
        }).fail(function (response) {
            $('#loadingElement').hide();
            renderRetryError(resources.unableToRetrieveDeviceFromService, $('#details_grid_container'), function () { getDeviceDetailsView(deviceId); });
        });
    }

    var getCellularDetailsView = function () {
        $('#loadingElement').show();

        var iccid = IoTApp.Helpers.IccidState.getIccidFromCookie();
        if (iccid == null) {
            renderRetryError(resources.unableToRetrieveDeviceFromService, $('#details_grid_container'), function () { getDeviceDetailsView(deviceId); });
            return;
        }

        $.get('/Device/GetDeviceCellularDetails', { iccid: iccid }, function (response) {
            onCellularDetailsDone(response);
        }).fail(function (response) {
            $('#loadingElement').hide();
            renderRetryError(resources.unableToRetrieveDeviceFromService, $('#details_grid_container'), function () { getDeviceDetailsView(deviceId); });
        });

    }

    var onCellularDetailsDone = function (html) {
        $('#loadingElement').hide();
        $('#details_grid_container').empty();
        $('#details_grid_container').html(html);

        $("#deviceExplorer_CellInformationBack").on("click", function () {
            $('#details_grid_container').empty();
            onDeviceDetailsDone(self.cachedDeviceHtml);
        });
    }

    var onDeviceDetailsDone = function (html) {

        if (self.cachedDeviceHtml == null) {
            self.cachedDeviceHtml = html;
        }

        $('#loadingElement').hide();
        $('#details_grid_container').empty();
        $('#details_grid_container').html(html);

        IoTApp.Helpers.Dates.localizeDates();

        setDetailsPaneLoaderHeight();

        $("#deviceExplorer_cellInformation").on("click", function () {
            $('#details_grid_container').empty();
            getCellularDetailsView();
        });

        $('#deviceExplorer_authKeys').on('click', function () {
            getDeviceKeys(self.deviceId);
        });

        $("#deviceExplorer_deactivateDevice").on("click", function () {

            var anchor = $(this);
            var isEnabled = anchor.data('hubenabledstate');
            isEnabled = !isEnabled;

            $.when(updateDeviceStatus(self.deviceId, isEnabled)).done(function (result) {
                var data = result.data;
                if (result.error || !data) {
                    IoTApp.Helpers.Dialog.displayError(resources.FailedToUpdateDeviceStatus);
                    return;
                }

                var deviceTable = $('#deviceTable').dataTable();
                var selectedTableRowStatus = deviceTable.find('.selected').find('td:eq(0)');

                if (isEnabled) {
                    _enableDisableDetailsLinks(true);
                    selectedTableRowStatus.removeClass('status_false');
                    selectedTableRowStatus.addClass('status_true');
                    selectedTableRowStatus.html(resources.running);
                    anchor.html(resources.deactivateDevice);
                } else {
                    _enableDisableDetailsLinks(false);
                    selectedTableRowStatus.removeClass('status_true');
                    selectedTableRowStatus.addClass('status_false');
                    selectedTableRowStatus.html(resources.disabled);
                    anchor.html(resources.activateDevice);
                }

                var hubDetailsField = $("#deviceDetailsGrid > [name=deviceField_HubEnabledState]");
                if (hubDetailsField) {
                    hubDetailsField.text(isEnabled ? "True" : "False");
                }

                anchor.data('hubenabledstate', isEnabled);
            }).fail(function () {
                IoTApp.Helpers.Dialog.displayError(resources.failedToUpdateDeviceStatus);
            });

            return false;
        });

        $("#deviceExplorer_removeSimAssociation").on("click", function () {
            $.ajax({
                url: '/Advanced/RemoveIccidFromDevice',
                data: { deviceId: self.deviceId },
                async: true,
                type: "post",
                success: function () {
                    getDeviceDetailsView(self.deviceId);
                }
            });
        });
    }

    var setDetailsPaneLoaderHeight = function () {
        /* Set the height of the Device Details progress animation background to accommodate scrolling */
        var progressAnimationHeight = $("#details_grid_container").height() + $(".details_grid__grid_subhead.button_details_grid").outerHeight();

        $(".loader_container_details").height(progressAnimationHeight);
    };

    var _enableDisableDetailsLinks = function (enabled) {
        if (enabled) {
            $(".link_grid_subheadhead_detail").removeClass("hidden");
            $("#edit_metadata_link").show();
            $('#editConfigLink').show();
            $('#removeDeviceLink').hide();
        } else {
            $(".link_grid_subheadhead_detail").addClass("hidden");
            $("#edit_metadata_link").hide();
            $('#editConfigLink').hide();
            $('#removeDeviceLink').show();
        }
    }

    var updateDeviceStatus = function (deviceId, isEnabled) {
        $('#loadingElement').show();
        var url = "/api/v1/devices/" + self.deviceId + "/enabledstatus";
        var data = {
            deviceId: self.deviceId,
            isEnabled: isEnabled
        };
        return $.ajax({
            url: url,
            type: 'PUT',
            data: data,
            dataType: 'json',
            success: function (result) {
                $('#loadingElement').hide();
                return result.data;
            },
            error: function () {
                $('#loadingElement').hide();
            }
        });
    }

    var getDeviceKeys = function (deviceId) {
        $('#loadingElement').show();
        $.get('/Device/GetDeviceKeys', { deviceId: deviceId }, function (response) {
            onDeviceKeysDone(response);
            // details pane just got longer--make the spinner fully cover it
            setDetailsPaneLoaderHeight();
        }).fail(function () {
            $('#loadingElement').hide();
            IoTApp.Helpers.Dialog.displayError(resources.errorWhileRetrievingKeys);
        });
    }

    var onRequestComplete = function onRequestComplete(requestObj, status) {
        if (timerId) {
            clearTimeout(timerId);
            timerId = null;
        }

        if (refreshMilliseconds) {
            timerId = setTimeout(refreshData, refreshMilliseconds);
        }
    };

    var refreshData = function refreshData() {
        if (telemetryDataUrl) {

            $.ajax({
                cache: false,
                complete: onRequestComplete,
                url: telemetryDataUrl
            }).done(
                function telemetryReadDone(data) {
                    if (data.deviceTelemetryModels &&
                        data.deviceTelemetryModels.length > 0) {
                        var lastObject = data.deviceTelemetryModels[data.deviceTelemetryModels.length - 1];
                        if (Object.keys(lastObject.boolValues).indexOf("cameraStatus") > -1) {
                            var status = lastObject.boolValues.cameraStatus;
                            if (status === true) {
                                $("#deviceExplorer_videofeed").removeClass('disable').addClass('not_disable');
                            } else {
                                $("#deviceExplorer_videofeed").removeClass('not_disable').addClass('disable');
                            }
                        }
                    } else {
                        $("#deviceExplorer_videofeed").removeClass('not_disable').addClass('disable');
                    }
                }
            ).fail(function () {
                if (timerId) {
                    clearTimeout(timerId);
                    timerId = null;
                }

                IoTApp.Helpers.Dialog.displayError(resources.unableToRetrieveDeviceTelemetryFromService);

                if (refreshMilliseconds) {
                    timerId = setTimeout(refreshData, refreshMilliseconds);
                }
            });
        }
    };

    var onDeviceKeysDone = function (html) {
        $('#loadingElement').hide();
        $('.deviceExplorer_detailLevel_authKeys').remove();
        $('#deviceExplorer_authKeys').parent().html(html);
    };

    var renderRetryError = function (errorMessage, container, retryCallback) {
        var $wrapper = $('<div />');
        var $paragraph = $('<p />');

        $wrapper.addClass('device_detail_error');
        $wrapper.append($paragraph);
        var node = document.createTextNode(errorMessage);
        $paragraph.append(node);
        $paragraph.addClass('device_detail_error__information');

        var button = $('<button class="button_base device_detail_error__retry_button">' + resources.retry + '</button>');

        button.on("click", function () {
            retryCallback();
        });

        $wrapper.append(button);
        container.html($wrapper);
    }

    var closeVideoWs = function () {
        if (wstimer) {
            clearTimeout(wstimer);
            wstimer = null;
        }

        if (webSocketVideoFrame) {
            try {
                webSocketVideoFrame.close();
                webSocketVideoFrame = null;
            } catch (e) {
                console.log(e);
            }
        }
    };

    var getVideoFrames = function (deviceId) {
        var wsendpoint = "wss://aucoveidemo.azurewebsites.net/api/v1/videoframes/receiver?deviceid=" + deviceId;

        webSocketVideoFrame = new WebSocket(wsendpoint);
        webSocketVideoFrame.binaryType = "arraybuffer";

        webSocketVideoFrame.onopen = function () {
            $('#videoloading').text("Connected! Loading video feed...");
        };

        webSocketVideoFrame.onclose = function () {
            $('#videoloading').text("Disconnected...");
        };

        webSocketVideoFrame.onmessage = function () {
            var bytearray = new Uint8Array(event.data);
            var blob = new Blob([event.data], { type: "image/jpeg" });
            lastImageUrl = createObjectURL(blob);

            $("#videoFrame").on("load",
                function () {
                    $('#videoFrame').show();
                    $('#videoloading').hide();
                    URL.revokeObjectURL(lastImageUrl);
                }).attr("src", lastImageUrl);

            frameTime = new Date().getTime();
        };
    };

    var createObjectURL = function (blob) {
        var URL = window.URL || window.webkitURL;
        if (URL && URL.createObjectURL) {
            return URL.createObjectURL(blob);
        } else {
            return null;
        }
    };

    var keepAliveGetVideoFrames = function (deviceId) {
        var duration = 0;

        if (frameTime !== undefined) {
            duration = new Date().getTime() - frameTime;
        }

        if (frameTime !== undefined && duration <= 1000) {

            wstimer = setTimeout(function () {
                keepAliveGetVideoFrames(deviceId);
            },
                100);
        } else {
            $('#videoFrame').attr("src", '/');
            $('#videoFrame').hide();
            $('#videoloading').show();

            getVideoFrames(deviceId);

            wstimer = setTimeout(function () {
                keepAliveGetVideoFrames(deviceId);
            },
                4000);
        }
    };

    var showDialog = function (deviceId) {
        $("#dialog").removeClass('hideVideoblock').addClass('showVideoblock');
        $('#dialog').dialog(
            {
                dialogClass: "no-close",
                modal: true,
                width: 350,
                autoOpen: true,
                open: function () {
                    closeVideoWs();
                    $('#videoFrame').attr("src", '/');
                    $('#videoFrame').hide();
                    $('#videoloading').text("Connecting...");
                    $('#videoloading').show();
                    keepAliveGetVideoFrames(deviceId);
                },
                close: function () {
                    $('#videoFrame').hide();
                    $("#dialog").removeClass('showVideoblock').addClass('hideVideoblock');
                    $('#videoloading').show();
                },
                buttons: {
                    Close: function () {
                        closeVideoWs();
                        var dtimer = setTimeout(
                            (function () {
                                console.log('1');
                                // empty;
                            })(),
                            1000);

                        if (dtimer) {
                            console.log('2');
                            $(this).dialog("close");
                            clearTimeout(dtimer);
                            dtimer = null;
                        }
                    }
                }
            });
    };

    return {
        init: function (deviceId, settings) {
            loadDataUrlBase = settings.loadDataUrlBase;
            refreshMilliseconds = settings.refreshMilliseconds;
            getDeviceDetailsView(deviceId);
        },

        showDialog: function (deviceId) {
            showDialog(deviceId);
        }
    }
}, [jQuery, resources]);
