function showAddInsuranceRecordModal() {
    $.get('/Vehicle/GetAddInsuranceRecordPartialView', function (data) {
        $("#insuranceRecordModalContent").html(data);
        configureInsuranceDatePickers();
        initTagSelector($("#insuranceTag"));
        $("#insuranceRecordModal").modal('show');
    });
}

function showEditInsuranceRecordModal(insuranceRecordId, invalidCache) {
    $.get(`/Vehicle/GetInsuranceRecordForEditById?insuranceRecordId=${insuranceRecordId}`, function (data) {
        $("#insuranceRecordModalContent").html(data);
        configureInsuranceDatePickers();
        initTagSelector($("#insuranceTag"));
        if (invalidCache) {
            $(".cached-banner").show();
        }
        $("#insuranceRecordModal").modal('show');
    });
}

function hideAddInsuranceRecordModal() {
    $("#insuranceRecordModal").modal('hide');
}

function configureInsuranceDatePickers() {
    var datePattern = getShortDatePattern().pattern;
    var weekStart = getGlobalConfig().firstDayOfWeek;
    $("#insuranceStartDate").datepicker({ format: datePattern, autoclose: true, todayHighlight: true, weekStart: weekStart });
    $("#insuranceExpiryDate").datepicker({ format: datePattern, autoclose: true, todayHighlight: true, weekStart: weekStart });
}

function saveInsuranceRecordToVehicle(isEdit) {
    var vehicleId = GetVehicleId().vehicleId;
    var policyNumber = $("#insurancePolicyNumber").val().trim();
    var insurerName = $("#insuranceInsurerName").val().trim();
    var policyType = $("#insurancePolicyType").val();
    var premium = globalParseFloat($("#insurancePremium").val());
    var startDate = $("#insuranceStartDate").val().trim();
    var expiryDate = $("#insuranceExpiryDate").val().trim();
    var notes = $("#insuranceNotes").val().trim();
    var tags = $("#insuranceTag").val();
    var reminderRecordId = recurringReminderRecordId;

    if (!policyNumber) {
        errorToast(getTranslation("Policy Number is required"));
        return;
    }
    if (!startDate || !expiryDate) {
        errorToast(getTranslation("Start Date and Expiry Date are required"));
        return;
    }

    var pendingFiles = getPendingFiles();
    var existingFiles = getExistingFiles();
    var insuranceRecordId = isEdit ? getInsuranceRecordModelData().id : 0;

    var insuranceRecordInput = {
        id: insuranceRecordId,
        vehicleId: vehicleId,
        policyNumber: policyNumber,
        insurerName: insurerName,
        policyType: policyType,
        premium: isNaN(premium) ? 0 : premium,
        startDate: startDate,
        expiryDate: expiryDate,
        notes: notes,
        files: existingFiles,
        tags: tags ? tags : [],
        reminderRecordId: reminderRecordId
    };

    if (pendingFiles.length > 0) {
        uploadFilesAndSave(pendingFiles, insuranceRecordInput, '/Vehicle/SaveInsuranceRecordToVehicleId', function (updatedInput) {
            doSaveInsurance(updatedInput, isEdit);
        });
    } else {
        doSaveInsurance(insuranceRecordInput, isEdit);
    }

    if (!isEdit && $("#addInsuranceReminderCheck").is(':checked')) {
        addReminderForInsurance(vehicleId, policyNumber, expiryDate);
    }
}

function doSaveInsurance(data, isEdit) {
    sloader.show();
    $.post('/Vehicle/SaveInsuranceRecordToVehicleId', data, function (response) {
        sloader.hide();
        if (response.success) {
            hideAddInsuranceRecordModal();
            getVehicleInsuranceRecords(GetVehicleId().vehicleId);
        } else {
            errorToast(response.message || genericErrorMessage());
        }
    });
}

function addReminderForInsurance(vehicleId, description, expiryDate) {
    var reminderData = {
        vehicleId: vehicleId,
        description: "Insurance Renewal: " + description,
        date: expiryDate,
        metric: "Date",
        isRecurring: false
    };
    $.post('/Vehicle/SaveReminderRecordToVehicleId', reminderData);
}

function deleteInsuranceRecord(insuranceRecordId) {
    Swal.fire({
        title: getTranslation("Delete Insurance Record?"),
        text: getTranslation("This action cannot be undone."),
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        confirmButtonText: getTranslation("Delete")
    }).then((result) => {
        if (result.isConfirmed) {
            $.post(`/Vehicle/DeleteInsuranceRecordById?insuranceRecordId=${insuranceRecordId}`, function (response) {
                if (response.success) {
                    hideAddInsuranceRecordModal();
                    getVehicleInsuranceRecords(GetVehicleId().vehicleId);
                } else {
                    errorToast(response.message || genericErrorMessage());
                }
            });
        }
    });
}
