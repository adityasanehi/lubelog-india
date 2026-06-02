function showAddPUCRecordModal() {
    $.get('/Vehicle/GetAddPUCRecordPartialView', function (data) {
        $("#pucRecordModalContent").html(data);
        configurePUCDatePickers();
        initTagSelector($("#pucTag"));
        $("#pucRecordModal").modal('show');
    });
}

function showEditPUCRecordModal(pucRecordId, invalidCache) {
    $.get(`/Vehicle/GetPUCRecordForEditById?pucRecordId=${pucRecordId}`, function (data) {
        $("#pucRecordModalContent").html(data);
        configurePUCDatePickers();
        initTagSelector($("#pucTag"));
        if (invalidCache) {
            $(".cached-banner").show();
        }
        $("#pucRecordModal").modal('show');
    });
}

function hideAddPUCRecordModal() {
    $("#pucRecordModal").modal('hide');
}

function configurePUCDatePickers() {
    var datePattern = getShortDatePattern().pattern;
    var weekStart = getGlobalConfig().firstDayOfWeek;
    $("#pucTestDate").datepicker({ format: datePattern, autoclose: true, todayHighlight: true, weekStart: weekStart });
    $("#pucExpiryDate").datepicker({ format: datePattern, autoclose: true, todayHighlight: true, weekStart: weekStart });
}

function savePUCRecordToVehicle(isEdit) {
    var vehicleId = GetVehicleId().vehicleId;
    var certificateNumber = $("#pucCertificateNumber").val().trim();
    var testCenter = $("#pucTestCenter").val().trim();
    var testDate = $("#pucTestDate").val().trim();
    var expiryDate = $("#pucExpiryDate").val().trim();
    var notes = $("#pucNotes").val().trim();
    var tags = $("#pucTag").val();
    var reminderRecordId = recurringReminderRecordId;

    if (!testDate || !expiryDate) {
        errorToast(getTranslation("Test Date and Expiry Date are required"));
        return;
    }

    var pucRecordId = isEdit ? getPUCRecordModelData().id : 0;

    var pucRecordInput = {
        id: pucRecordId,
        vehicleId: vehicleId,
        certificateNumber: certificateNumber,
        testCenter: testCenter,
        testDate: testDate,
        expiryDate: expiryDate,
        notes: notes,
        files: uploadedFiles,
        tags: tags ? tags : [],
        reminderRecordId: reminderRecordId
    };

    doSavePUC(pucRecordInput, isEdit);

    if (!isEdit && $("#addPUCReminderCheck").is(':checked')) {
        addReminderForPUC(vehicleId, certificateNumber, expiryDate);
    }
}

function doSavePUC(data, isEdit) {
    sloader.show();
    $.post('/Vehicle/SavePUCRecordToVehicleId', data, function (response) {
        sloader.hide();
        if (response.success) {
            hideAddPUCRecordModal();
            getVehiclePUCRecords(GetVehicleId().vehicleId);
        } else {
            errorToast(response.message || genericErrorMessage());
        }
    });
}

function addReminderForPUC(vehicleId, certificateNumber, expiryDate) {
    var reminderData = {
        vehicleId: vehicleId,
        description: "PUC Renewal" + (certificateNumber ? ": " + certificateNumber : ""),
        date: expiryDate,
        metric: "Date",
        isRecurring: true,
        reminderMonthInterval: "OneYear"
    };
    $.post('/Vehicle/SaveReminderRecordToVehicleId', reminderData);
}

function deletePUCRecord(pucRecordId) {
    Swal.fire({
        title: getTranslation("Delete PUC Record?"),
        text: getTranslation("This action cannot be undone."),
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        confirmButtonText: getTranslation("Delete")
    }).then((result) => {
        if (result.isConfirmed) {
            $.post(`/Vehicle/DeletePUCRecordById?pucRecordId=${pucRecordId}`, function (response) {
                if (response.success) {
                    hideAddPUCRecordModal();
                    getVehiclePUCRecords(GetVehicleId().vehicleId);
                } else {
                    errorToast(response.message || genericErrorMessage());
                }
            });
        }
    });
}
