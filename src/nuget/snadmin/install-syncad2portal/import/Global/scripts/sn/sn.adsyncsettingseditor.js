// using $skin/scripts/sn/sn.controls.js
// resource ADSync

(function ($) {
    $.ADSyncSettingsEditor = function (el, options) {
        var aDSyncSettingsEditor = this;
        aDSyncSettingsEditor.$el = $(el);
        aDSyncSettingsEditor.el = el;
        if (aDSyncSettingsEditor.$el.data('ADSyncSettingsEditor'))
            return;

        var x = aDSyncSettingsEditor.$el.val();
        var data, rawdata, $form;
        aDSyncSettingsEditor.init = function () {
            aDSyncSettingsEditor.$el.hide();
            $form = SN.Controls.Form.render();
            aDSyncSettingsEditor.$el.after($form);
            if (x !== '') {
                if (IsJsonString(x)) {
                    data = JSON.parse(x);
                    rawdata = JSON.parse(x);
                    $.each(data, function (key, value) {
                        if (typeof data[key] === 'boolean') {
                            SN.Controls.Boolean.render($form, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: data[key],
                                save: setBooleanValue,
                                data: rawdata,
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                            if (key === 'Enabled')
                                buildEnablingInfo($('#chekbox-Enabled'));
                        }
                        else if (typeof data[key] === 'number') {
                            SN.Controls.Number.render($form, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: data[key],
                                extraClass: null,
                                required: false,
                                save: setNumberValue,
                                data: rawdata,
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                        }
                        else if (typeof data[key] === 'string') {
                            SN.Controls.String.render($form, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: data[key],
                                extraClass: null,
                                required: false,
                                save: setStringValue,
                                data: rawdata,
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                        }
                        else {
                            if (key === 'Servers') {
                                buildServersGrid(data[key]);
                            }
                            else if (key === 'SyncTrees') {
                                buildSyncTrees(data[key]);
                            }
                            else if (key === 'MappingDefinitions') {
                                buildMappingDefinitions(data[key]);
                            }
                            else if (key === 'Scheduling') {
                                buildScheduling(data[key]);
                            }
                        }
                    });

                    var validator = $form.kendoValidator({
                        messages: {
                            required: SN.Resources.ADSync["RequiredField"]
                        },
                    }).data("kendoValidator");

                }
                else {
                    $.error('SyntaxError!');
                }
            }
            else {
                data = {
                    "Enabled": false,
                    "Scheduling": {},
                    "ParallelOperations": 0,
                    "Servers": [],
                    "SyncTrees": [],
                    "MappingDefinitions": []
                };
                rawdata = {
                    "Enabled": false,
                    "Scheduling": {},
                    "ParallelOperations": 0,
                    "Servers": [],
                    "SyncTrees": [],
                    "MappingDefinitions": []
                };
                SN.Controls.Boolean.render($form, {
                    label: SN.Resources.ADSync["Enabled-DisplayName"],
                    value: "Enabled",
                    save: setBooleanValue,
                    data: data,
                    info: SN.Resources.ADSync["Enabled-Description"],
                    key: "Enabled"
                });
                buildEnablingInfo($('#chekbox-Enabled'));
                buildScheduling("Scheduling");
                SN.Controls.Number.render(
                    $form,
                    SN.Resources.ADSync["ParallelOperations-DisplayName"],
                    data["ParallelOperations"],
                    null,
                    false,
                    setNumberValue,
                    rawdata.ParallelOperations,
                    SN.Resources.ADSync["ParallelOperations-Description"],
                    "ParallelOperations");
                buildServersGrid("Servers");
                buildSyncTrees("SyncTrees");
                buildMappingDefinitions("MappingDefinitions");
            }
        }

        function buildScheduling(d) {
            SN.Controls.H1.render($form,
                {
                    label: SN.Resources.ADSync["Scheduling-DisplayName"],
                    info: SN.Resources.ADSync["Scheduling-Description"]
                });
            SN.Controls.FieldGroup.render($form, 'scheduling-group');
            var $group = $('#scheduling-group');
            if (typeof d !== 'string') {
                var frq = d["Frequency"] || '';
                var exact = d["ExactTime"] || '';
                SN.Controls.Number.render($group, {
                    label: SN.Resources.ADSync["Frequency-DisplayName"],
                    value: frq,
                    extraClass: null,
                    required: false,
                    save: setNumberValue,
                    data: rawdata.Scheduling,
                    info: SN.Resources.ADSync["Frequency-Description"],
                    key: "Frequency"
                });
                SN.Controls.String.render($group, {
                    label: SN.Resources.ADSync["ExactTime-DisplayName"],
                    value: exact,
                    extraClass: 'timefield',
                    required: false,
                    save: setStringValue,
                    data: rawdata.Scheduling,
                    info: SN.Resources.ADSync["ExactTime-Description"],
                    key: "ExactTime",
                });
                SN.Controls.Relationship.Xor($group);
            }
            else {
                SN.Controls.Number.render($group, {
                    label: SN.Resources.ADSync["Frequency-DisplayName"],
                    value: '',
                    extraClass: null,
                    required: false,
                    save: setNumberValue,
                    data: rawdata.Scheduling,
                    info: SN.Resources.ADSync["Frequency-Description"],
                    key: "Frequency"
                });
                SN.Controls.String.render($group, {
                    label: SN.Resources.ADSync["ExactTime-DisplayName"],
                    value: '',
                    extraClass: null,
                    required: false,
                    save: setStringValue,
                    data: rawdata.Scheduling,
                    info: SN.Resources.ADSync["ExactTime-Description"],
                    key: "ExactTime"
                });
                SN.Controls.Relationship.Xor($group);
            }
            $('.timefield').kendoTimePicker({
                format: 'HH:mm',
                change: function (e) {
                    var $this = e.sender.element;
                    $($this).closest('div').siblings('div').find('input').val('');
                }
            });

        }

        function buildServersGrid(d) {
            SN.Controls.H1.render($form, {
                label: SN.Resources.ADSync["ServersGrid-DisplayName"]
            });
            var columns = [];
            var fields = {};
            if (typeof d !== 'string' && d.length > 0) {
                $.each(d[0], function (key, value) {
                    var sr = key + '-DisplayName';
                    var desc = key + '-Description';

                    if (typeof value === 'object')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 150, aDSyncSettingsEditor.credentialsTemplate, aDSyncSettingsEditor.credentialEditor));
                    else if (key === 'DeletedPortalObjectsPath')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 150));
                    else if (typeof value === 'number')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 50, aDSyncSettingsEditor[template], aDSyncSettingsEditor.NumberEditor));
                    else if (typeof value === 'boolean') {
                        var template = key + "Template";
                        if (typeof SN.Resources.ADSync[desc] !== 'undefined')
                            columns.push(new column(key, SN.Resources.ADSync[sr], 50, aDSyncSettingsEditor.BooleanTemplate(key), aDSyncSettingsEditor.BooleanEditor));
                        else
                            columns.push(new column(key, SN.Resources.ADSync[sr], 50, aDSyncSettingsEditor.BooleanTemplate(key)));
                    }
                    else if (key === 'Id')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 150));
                    else {
                        columns.push(new column(key, SN.Resources.ADSync[sr], 70, null, aDSyncSettingsEditor.StringEditor));
                    }

                    if (typeof value === 'object') {
                        var credentials = { Username: "", Password: "", Anonymous: false };
                        fields[key] = { type: typeof value, defaultValue: credentials }
                    }
                    else {
                        fields[key] = { type: typeof value }
                        if (key === 'Name' || key === 'LdapServer') {
                            fields[key].validation = {};
                            fields[key].validation.required = true;
                        }
                        else if (key === 'DeletedPortalObjectsPath') {
                            fields[key].validation = {};
                            fields[key].validation.required = true;
                            fields[key].defaultValue = '/Root/IMS/Deleted';
                        }
                        else if (key === 'SyncEnabledState' || key === 'SyncUserName') {
                            fields[key].defaultValue = true;
                        }
                        else if (key === 'Port') {
                            fields[key].defaultValue = 389;
                        }
                    }
                });
                columns.push({ command: [{ name: "edit", text: '<span class="fa fa-pencil"></span>' }, { name: "delete", text: '<span class="fa fa-remove"></span>' }], title: "&nbsp;" });
            }
            else {
                columns.push(new column("Name", SN.Resources.ADSync["Name-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("LdapServer", SN.Resources.ADSync["LdapServer-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("Novell", SN.Resources.ADSync["Novell-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('Novell')));
                columns.push(new column("LogonCredentials", SN.Resources.ADSync["LogonCredentials-DisplayName"], 150, aDSyncSettingsEditor.credentialsTemplate, aDSyncSettingsEditor.credentialEditor));
                columns.push(new column("UseSsl", SN.Resources.ADSync["UseSsl-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('UseSsl')));
                //columns.push(new column("UseSasl", SN.Resources.ADSync["UseSasl-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('UseSasl')));
                columns.push(new column("Port", SN.Resources.ADSync["Port-DisplayName"], 60, aDSyncSettingsEditor["Port-Template"], "#"));
                //columns.push(new column("TrustWrongCertification", SN.Resources.ADSync["TrustWrongCertification-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('TrustWrongCertification')));
                columns.push(new column("SyncEnabledState", SN.Resources.ADSync["SyncEnabledState-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('SyncEnabledState')));
                columns.push(new column("SyncUserName", SN.Resources.ADSync["SyncUserName-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('SyncUserName')));
                columns.push(new column("DeletedPortalObjectsPath", SN.Resources.ADSync["DeletedPortalObjectsPath-DisplayName"], 150));
                columns.push(new column("UserType", SN.Resources.ADSync["UserType-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("Id", SN.Resources.ADSync["Id-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push({ command: [{ name: "edit", text: SN.Resources.ADSync["EditServer"] }, { name: "delete", text: SN.Resources.ADSync["DeleteServer"] }], title: "&nbsp;" });

                var credentials = { Username: "", Password: "", Anonymous: false };
                var defaultPort = 389;
                var newId = createGuid();

                fields.Name = { type: "string", editable: true, validation: { required: true } },
                fields.LdapServer = { type: "string" },
                fields.Novell = { type: "boolean" },
                fields.LogonCredentials = { type: "object", defaultValue: credentials },
                fields.UseSsl = { type: "boolean" },
                //fields.UseSasl = { type: "boolean" },
                fields.Port = { type: "number", defaultValue: defaultPort },
                //fields.TrustWrongCertification = { type: "boolean" },
                fields.SyncEnabledState = { type: "boolean", defaultValue: true },
                fields.SyncUserName = { type: "boolean", defaultValue: true },
                fields.DeletedPortalObjectsPath = { type: "string", validation: { required: true } },
                fields.UserType = { type: "string" };
                fields.Id = { type: "string", defaultValue: newId, editable: false, editable: false, visible: false };

                d = [];
            }
            var toolbar = [{ name: "create", text: SN.Resources.ADSync["AddNewServer"] }];

            SN.Controls.Grid.render($form, {
                dataSource: d,
                columns: columns,
                fields: fields,
                save: saveServer,
                remove: removeServer,
                toolbar: toolbar,
                id: "Name",
                gridId: "Servers",
                detailTemplate: aDSyncSettingsEditor.serverDetailTemplate,
                edit: function (e) {
                    e.container.find('label[for="Id"]').parent().hide();
                    e.container.find('div[data-container-for="Id"]').hide();
                    if (e.model.isNew()) {
                        e.model.set("Id", createGuid());
                    }
                }
            });

            setTimeout(function () {
                var grid = $('#Servers').data("kendoGrid");
                grid.hideColumn("UseSsl");
                grid.hideColumn("UseSasl")
                grid.hideColumn("Port");
                grid.hideColumn("Id");
                grid.hideColumn("TrustWrongCertification");
            }, 500);
        }

        function buildSyncTrees(d) {
            SN.Controls.H1.render($form, { label: SN.Resources.ADSync["SyncTreesGrid-DisplayName"] });
            var columns = [];
            var fields = {};
            if (typeof d !== 'string') {
                $.each(d[0], function (key, value) {
                    var sr = key + '-DisplayName';
                    var desc = key + '-Description';

                    if (key === 'Mappings') {
                        columns.push(new column(key, SN.Resources.ADSync[sr], 150, null, aDSyncSettingsEditor.mappingsEditor));
                    }
                    else if (key === 'Server') {
                        columns.push(new column(key, SN.Resources.ADSync[sr], 70, null, aDSyncSettingsEditor.serverEditor));
                    }
                    else if (typeof value === 'object')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 150, aDSyncSettingsEditor.aDExceptionsTemplate, aDSyncSettingsEditor.aDExceptionsEditor));
                    else if (typeof value === 'number')
                        columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor[template], null));
                    else if (typeof value === 'boolean') {
                        var template = key + "Template";
                        columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor.BooleanTemplate(key)));
                    }
                    else {
                        if (typeof SN.Resources.ADSync[desc] !== 'undefined')
                            columns.push(new column(key, SN.Resources.ADSync[sr], 70, null, aDSyncSettingsEditor.StringEditor));
                        else
                            columns.push(new column(key, SN.Resources.ADSync[sr], 70));
                    }

                    fields[key] = { type: typeof value }

                    if (key === 'Server' || key === 'BaseDn' || key === 'PortalPath' || key === 'Mappings') {
                        fields[key].validation = {};
                        fields[key].validation.required = true;
                    }
                    if (key === 'ADExceptions')
                        fields[key].defaultValue = [];
                    if (key === 'UserFilter' || key === 'GroupFilter' || key === 'ContainerFilter')
                        fields[key].defaultValue = '*';
                });
                columns.push({ command: [{ name: "edit", text: '<span class="fa fa-pencil"></span>' }, { name: "delete", text: '<span class="fa fa-remove"></span>' }], title: "&nbsp;" });

                for (var i = 0; i < d.length; i++) {
                    d[i].Id = createGuid();
                }
            }
            else {

                columns.push(new column("Server", SN.Resources.ADSync["Name-Server"], 70));
                columns.push(new column("BaseDN", SN.Resources.ADSync["BaseDN-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("PortalPath", SN.Resources.ADSync["PortalPath-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("ADExceptions", SN.Resources.ADSync["ADExceptions-DisplayName"], 150));
                columns.push(new column("UserFilter", SN.Resources.ADSync["UserFilter-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("GroupFilter", SN.Resources.ADSync["GroupFilter-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("ContainerFilter", SN.Resources.ADSync["ContainerFilter-DisplayName"], 70, null, aDSyncSettingsEditor.StringEditor));
                columns.push(new column("SyncGroups", SN.Resources.ADSync["SyncGroups-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('SyncGroups')));
                columns.push(new column("SyncPhotos", SN.Resources.ADSync["SyncPhotos-DisplayName"], 60, aDSyncSettingsEditor.BooleanTemplate('SyncPhotos')));
                columns.push({ command: [{ name: "edit", text: SN.Resources.ADSync["EditServer"] }, { name: "delete", text: SN.Resources.ADSync["DeleteServer"] }], title: "&nbsp;" });

                var credentials = { Username: "", Password: "", Anonymous: false };
                fields.Server = { type: "string", validation: { required: true } },
                fields.BaseDN = { type: "string" },
                fields.PortalPath = { type: "string", validation: { required: true } },
                fields.Mappings = { type: "string", validation: { required: true } },
                fields.ADExceptions = { type: "object", defaultValue: [] },
                fields.UserFilter = { type: "string", defaultValue: '*' },
                fields.GroupFilter = { type: "string", defaultValue: '*' },
                fields.ContainerFilter = { type: "string", defaultValue: '*' },
                fields.SyncGroups = { type: "boolean" }
                fields.SyncPhotos = { type: "boolean" }

                d = [];
            }
            var toolbar = [{ name: "create", text: SN.Resources.ADSync["AddNewSyncTree"] }];

            SN.Controls.Grid.render($form, {
                dataSource: d,
                columns: columns,
                fields: fields,
                save: saveSyncTree,
                remove: removeSyncTree,
                toolbar: toolbar,
                id: "Server",
                gridId: "SyncTrees",
                detailTemplate: aDSyncSettingsEditor.syncTreeDetailTemplate
            });


            setTimeout(function () {
                var grid = $('#SyncTrees').data("kendoGrid");
                grid.hideColumn("UserFilter")
                grid.hideColumn("GroupFilter")
                grid.hideColumn("ContainerFilter");
                grid.hideColumn("Id");
            }, 500);
        }

        function buildMappingDefinitions(d) {
            SN.Controls.H1.render($form, { label: SN.Resources.ADSync["PropertyMappingDefinitions-DisplayName"] });
            var $list = (SN.Controls.List.render('sn-adsync-mapping-list')).appendTo($form);
            var $listItem;
            if (typeof d !== "string") {
                for (var i = 0; i < d.length; i++) {
                    $listItem = $('<div class="sn-adsync-mapping-listitem" id="sn-adsync-mapping-listitem-' + (i + 1) + '"></div>').appendTo($list);
                    $.each(d[i], function (key, value) {
                        if (typeof d[i][key] === 'boolean') {
                            SN.Controls.Boolean.render($listItem, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: d[i][key],
                                save: setBooleanValue,
                                data: d[i],
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                        }
                        else if (typeof d[i][key] === 'number') {
                            SN.Controls.Number.render($listItem, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: d[i][key],
                                save: setNumberValue,
                                data: d[i],
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                        }
                        else if (typeof d[i][key] === 'string') {
                            var required = false;
                            if (key === 'Name')
                                required = true;
                            SN.Controls.String.render($listItem, {
                                label: SN.Resources.ADSync[key + "-DisplayName"],
                                value: d[i][key],
                                extraClass: null,
                                required: required,
                                save: setStringValue,
                                data: d[i],
                                info: SN.Resources.ADSync[key + "-Description"],
                                key: key
                            });
                            if (key === "Name") {
                                var $openMappingDefinition = $listItem.find('.sn-formrow').first().append('<span class="sn-icon sn-icon-inline sn-icon-open fa fa-angle-down"></span>');
                                $openMappingDefinition.on('click', function () {
                                    var $that = $(this);
                                    var $mappingContainer = $that.closest('.sn-adsync-mapping-listitem').find('.sn-adsync-mapping-listitem-inner');
                                    if ($mappingContainer.hasClass('hidden')) {
                                        $mappingContainer.removeClass('hidden');
                                        $that.find('.sn-icon').removeClass('fa-angle-down').addClass('fa-angle-up');
                                    }
                                    else {
                                        if (fieldsAreValid()) {
                                            $mappingContainer.addClass('hidden');
                                            $that.find('.sn-icon').removeClass('fa-angle-up').addClass('fa-angle-down');
                                        }
                                    }
                                });
                            }
                        }
                        else {
                            buildMappingListItems(d[i][key], $listItem);
                        }
                    });
                }
            }
            var $addNewPropertyMappingDefinition = $('<a class="k-button k-button-icontext k-grid-add" href="#"><span class="k-icon k-add"></span>' + SN.Resources.ADSync["AddNewPropertyMappingDefinition"] + '</a>').appendTo($list);
            $addNewPropertyMappingDefinition.on('click', function (e) {
                e.preventDefault();
                addNewPropertyMappingDefinition($list, $addNewPropertyMappingDefinition);
            });
        }

        function addNewPropertyMappingDefinition($container, $button) {
            var $window = $('<div><div class="k-edit-form-container"><div class="k-edit-label"><label class="km-required" for="Name">Name</label></div><div class="k-edit-field">\
                <input required="required" type="text"id="Name" class="k-input k-textbox" name="Name"></div><div class="k-edit-buttons k-state-default"></div></div></div>');

            var $saveButton = $('<a class="k-button k-button-icontext k-primary k-grid-update" href="#"><span class="k-icon k-update"></span>Update</a>').appendTo($window.find('.k-edit-buttons'));
            var $cancelButton = $('<a class="k-button k-button-icontext k-grid-cancel" href="#"><span class="k-icon k-cancel"></span>Cancel</a>').appendTo($window.find('.k-edit-buttons'));

            $window.kendoWindow({
                width: "600px",
                title: SN.Resources.ADSync["AddNewPropertyMappingDefinition"],
                visible: false,
                modal: true
            }).data("kendoWindow").center().open();

            var dialog = $window.data("kendoWindow");
            var validator = $('input#Name').kendoValidator({
                messages: {
                    required: SN.Resources.ADSync["RequiredField"]
                },
            }).data("kendoValidator");
            $saveButton.on('click', function (e) {
                e.preventDefault();

                if (validator.validate()) {
                    var value = $window.find('input[name="Name"]').val();
                    rawdata.MappingDefinitions.push(new PropertyMappingDefinition(value, []));

                    var $listItem = $('<div class="sn-adsync-mapping-listitem"></div>').appendTo($container);

                    SN.Controls.String.render($listItem, {
                        label: SN.Resources.ADSync["Name-DisplayName"],
                        value: value,
                        extraClass: null,
                        required: true,
                        save: setStringValue,
                        data: data.MappingDefinitions[data.MappingDefinitions.length - 1],
                        info: SN.Resources.ADSync["Name-Description"],
                        key: "Name"
                    });
                    setStringValue(data.MappingDefinitions[data.MappingDefinitions.length - 1], SN.Resources.ADSync["Name-DisplayName"], value);
                    buildMappingListItems([], $listItem);

                    var $openMappingDefinition = $listItem.find('.sn-formrow').first().append('<span class="sn-icon sn-icon-open sn-icon-inline fa fa-angle-down"></span>');
                    $openMappingDefinition.on('click', function () {
                        var $that = $(this);
                        var $mappingContainer = $that.closest('.sn-adsync-mapping-listitem').find('.sn-adsync-mapping-listitem-inner');
                        if ($mappingContainer.hasClass('hidden')) {
                            $mappingContainer.removeClass('hidden');
                            $that.find('.sn-icon').removeClass('fa-angle-down').addClass('fa-angle-up');
                        }
                        else {
                            if (fieldsAreValid()) {
                                $mappingContainer.addClass('hidden');
                                $that.find('.sn-icon').removeClass('fa-angle-up').addClass('fa-angle-down');
                            }
                        }
                    });
                    $button.appendTo($container);
                    $openMappingDefinition.trigger('click');

                    dialog.close();
                    dialog.destroy();
                }
            });

            $cancelButton.on('click', function (e) {
                e.preventDefault();
                dialog.close();
                dialog.destroy();
            });

        }

        function buildMappingListItems(d, $list) {
            var $listItemInner = $('<div class="sn-adsync-mapping-listitem-inner hidden"></div>').appendTo($list);
            SN.Controls.H2.render($listItemInner, {
                title: SN.Resources.ADSync["PropertyMappings-DisplayName"],
                info: SN.Resources.ADSync["PropertyMappings-Description"]
            });

            if (d.length > 0) {
                $.each(d, function (i, item) {
                    var $innerList = (SN.Controls.List.render('sn-propertymapping')).appendTo($listItemInner);

                    var $title = $('<h3>' + SN.Resources.ADSync["PropertyMapping"] + (i + 1) + ' (' + getPropertyNames(item.ADProperties) + ')<span class="sn-icon sn-icon-open sn-icon-inline fa fa-angle-down"></span></h3>').prependTo($innerList);
                    var $mappingContainer = $('<div class="sn-mapping-grid-container hidden" id="sn-mapping-grid-container-' + (i + 1) + '"></div>').appendTo($innerList);
                    buildPropertyMapping($mappingContainer, item);

                    $title.find('.sn-icon').on('click', function () {
                        var $that = $(this);
                        var $container = $that.closest('.sn-inner-form.sn-propertymapping');
                        var $gridContainer = $container.find('.sn-mapping-grid-container');
                        if ($gridContainer.hasClass('hidden')) {
                            $gridContainer.removeClass('hidden');
                            $that.removeClass('fa-angle-down').addClass('fa-angle-up');
                        }
                        else {
                            if (fieldsAreValid()) {
                                $gridContainer.addClass('hidden');
                                $that.addClass('fa-angle-down').removeClass('fa-angle-up');
                            }
                        }
                    });
                });
            }

            var $addNewPropertyMapping = $('<a class="k-button k-button-icontext k-grid-add" href="#"><span class="k-icon k-add"></span>' + SN.Resources.ADSync["AddNewPropertyMapping"] + '</a>').appendTo($listItemInner)
            $addNewPropertyMapping.on('click', function (e) {
                e.preventDefault();
                addNewPropertyMapping($listItemInner, $addNewPropertyMapping);
            });
        }

        function addNewPropertyMapping($container, $button) {

            var $innerList = (SN.Controls.List.render('sn-propertymapping')).appendTo($container);
            var index = $container.find('.sn-propertymapping').length;

            var $title = $('<h3>' + SN.Resources.ADSync["PropertyMapping"] + (index) + ' ()<span class="sn-icon sn-icon-open sn-icon-inline fa fa-angle-down"></span></h3>').prependTo($innerList);
            var $mappingContainer = $('<div class="sn-mapping-grid-container hidden"></div>').appendTo($innerList);

            var mappingsDefinition = rawdata.MappingDefinitions[data.MappingDefinitions.length - 1];
            if (typeof mappingsDefinition === 'undefined' || mappingsDefinition.Mappings.length === 0)
                mappingsDefinition.Mappings = [];

            mappingsDefinition.Mappings.push({ 'Separator': '', 'ADProperties': [], 'PortalProperties': [] });

            buildPropertyMapping($mappingContainer, mappingsDefinition.Mappings[mappingsDefinition.Mappings.length - 1]);

            $title.find('.sn-icon').on('click', function () {
                var $that = $(this);
                var $container = $that.closest('.sn-inner-form.sn-propertymapping');
                var $gridContainer = $container.find('.sn-mapping-grid-container');
                if ($gridContainer.hasClass('hidden')) {
                    $gridContainer.removeClass('hidden');
                    $that.removeClass('fa-angle-down').addClass('fa-angle-up');
                }
                else {
                    if (fieldsAreValid()) {
                        $gridContainer.addClass('hidden');
                        $that.addClass('fa-angle-down').removeClass('fa-angle-up');
                    }
                }
            });

            $button.appendTo($container);
            $title.find('.sn-icon').trigger('click');


        }

        function buildPropertyMapping($container, d) {
            if (Object.keys(d).length > 1) {
                $.each(d, function (key, value) {

                    if (typeof d[key] === 'boolean') {
                        SN.Controls.Boolean.render($container, {
                            label: SN.Resources.ADSync[key + "-DisplayName"],
                            value: d[key],
                            save: setBooleanValue,
                            data: d,
                            info: SN.Resources.ADSync[key + "-Description"],
                            key: key
                        });
                    }
                    else if (typeof d[key] === 'number') {
                        SN.Controls.Number.render($container, {
                            label: SN.Resources.ADSync[key + "-DisplayName"],
                            value: d[key],
                            save: setNumberValue,
                            data: d,
                            info: SN.Resources.ADSync[key + "-Description"],
                            key: key
                        });
                    }
                    else if (typeof d[key] === 'string') {
                        var required = false;
                        if (key === "Separator")
                            required = true;
                        var value = d[key];
                        SN.Controls.String.render($container, {
                            label: SN.Resources.ADSync[key + "-DisplayName"],
                            value: value,
                            extraClass: null,
                            required: required,
                            save: saveSeparator,
                            data: d,
                            info: SN.Resources.ADSync[key + "-Description"],
                            key: key
                        });
                        if (d[key].length === 0) {
                            var $mappingContainer = $container.closest('.sn-adsync-mapping-listitem-inner');
                            $container.removeClass('hidden');
                            $mappingContainer.removeClass('hidden');
                            $mappingContainer.closest('.sn-adsync-mapping-listitem').find('.sn-icon').removeClass('fa-angle-down').addClass('fa-angle-up');
                            $container.find('.sn-icon').removeClass('fa-angle-down').addClass('fa-angle-up');

                            var validator = $container.kendoValidator({
                                messages: {
                                    required: SN.Resources.ADSync["RequiredField"]
                                },
                            }).data("kendoValidator");
                            validator.validateInput($container.find('input[required]'));
                        }
                    }
                    else {
                        if (key === 'ADProperties') {
                            var ADPropertySchema = {
                                id: "Name",
                                fields: {
                                    Name: { type: "string", editable: true, validation: { required: true } },
                                    Unique: { type: "boolean", editable: true },
                                    MaxLength: { type: "number", editable: true }
                                }
                            }

                            for (var i = 0; i < d[key].length; i++) {
                                d[key][i].Id = createGuid();

                            }
                            buildADPropertiesGrid(d[key], $container, ADPropertySchema.fields, $('.sn-grid').length);
                        }
                        else if (key === 'PortalProperties') {
                            var PortalPropertySchema = {
                                id: "Name",
                                fields: {
                                    Name: { type: "string", editable: true, validation: { required: true } },
                                    Unique: { type: "boolean", editable: true },
                                    MaxLength: { type: "number", editable: true }
                                }
                            }

                            buildPortalPropertiesGrid(d[key], $container, PortalPropertySchema.fields, $('.sn-grid').length);
                        }
                    }
                });
            }
            else {
                SN.Controls.String.render($container, {
                    label: 'Separator',
                    value: '',
                    extraClass: null,
                    required: true,
                    save: setStringValue,
                    data: d,
                    info: SN.Resources.ADSync["Separator-Description"],
                    key: 'Separator'
                });
                var ADPropertySchema = {
                    id: "Name",
                    fields: {
                        Name: { type: "string", editable: true, validation: { required: true } },
                        Unique: { type: "boolean", editable: true },
                        MaxLength: { type: "number", editable: true }
                    }
                }
                buildADPropertiesGrid([], $container, ADPropertySchema.fields, $('.sn-grid').length);

                var PortalPropertySchema = {
                    id: "Name",
                    fields: {
                        Name: { type: "string", editable: true, validation: { required: true } },
                        Unique: { type: "boolean", editable: true },
                        MaxLength: { type: "number", editable: true }
                    }
                }
                buildPortalPropertiesGrid([], $container, PortalPropertySchema.fields, $('.sn-grid').length);

            }
        }

        function buildADPropertiesGrid(d, $container, fields, idNum) {
            var $gridRow = $('<div class="sn-gridrow"></div>').appendTo($container);
            SN.Controls.H3.render($gridRow, { label: SN.Resources.ADSync["ADProperties-DisplayName"] });
            var columns = [];
            var newData = setDefaultValue(d, [{ "Unique": false }, { "MaxLength": 0 }]);

            $.each(fields, function (key, value) {
                var sr = key + '-DisplayName';

                var template = key + "Template";
                if (value.type === 'number')
                    columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor[template], aDSyncSettingsEditor.NumberEditor));
                else if (value.type === 'boolean') {
                    columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor.BooleanTemplate(key), aDSyncSettingsEditor.BooleanEditor));
                }
                else if (key !== 'Id')
                    columns.push(new column(key, SN.Resources.ADSync[sr], 70, null, aDSyncSettingsEditor.StringEditor));
            });

            columns.push({ command: [{ name: "edit", text: '<span class="fa fa-pencil"></span>' }, { name: "delete", text: '<span class="fa fa-remove"></span>' }], title: "&nbsp;", width: "100px" });

            var toolbar = [{ name: "create", text: SN.Resources.ADSync["AddNewADProperty"] }];

            SN.Controls.Grid.render($gridRow, {
                dataSource: newData,
                columns: columns,
                fields: fields,
                save: saveADPropery,
                remove: removeAdProperty,
                toolbar: toolbar,
                id: "Name",
                gridId: 'grid-' + idNum
            });
        }

        function buildPortalPropertiesGrid(d, $container, fields, idNum) {
            var $gridRow = $('<div class="sn-gridrow"></div>').appendTo($container);
            SN.Controls.H3.render($gridRow, { label: SN.Resources.ADSync["PortalProperties-DisplayName"] });
            var columns = [];
            var newData = setDefaultValue(d, [{ "Unique": false }, { "MaxLength": 0 }]);

            $.each(fields, function (key, value) {
                var sr = key + '-DisplayName';
                var template = key + "Template";

                if (value.type === 'number')
                    columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor[template], aDSyncSettingsEditor.NumberEditor));
                else if (value.type === 'boolean') {
                    columns.push(new column(key, SN.Resources.ADSync[sr], 60, aDSyncSettingsEditor.BooleanTemplate(key), aDSyncSettingsEditor.uniqueEditor));
                }
                else if (key !== 'Id')
                    columns.push(new column(key, SN.Resources.ADSync[sr], 70, null, aDSyncSettingsEditor.StringEditor));
            });
            columns.push({ command: [{ name: "edit", text: '<span class="fa fa-pencil"></span>' }, { name: "delete", text: '<span class="fa fa-remove"></span>' }], title: "&nbsp;", width: "100px" });

            for (var i = 0; i < newData.length; i++) {
                newData[i].Id = createGuid();
            }

            var toolbar = [{ name: "create", text: SN.Resources.ADSync["AddNewPortalProperty"] }];

            SN.Controls.Grid.render($gridRow, {
                dataSource: newData,
                columns: columns,
                fields: fields,
                save: savePortalProperty,
                remove: removePortalProperty,
                toolbar: toolbar,
                id: "Name",
                gridId: 'grid-' + idNum
            });
        }

        function getPropertyNames(d) {
            var text = '';
            for (var i = 0; i < d.length; i++) {
                if (i < d.length - 1)
                    text += d[i].Name + ', ';
                else
                    text += d[i].Name;
            }
            return text;
        }

        function PropertyMappingDefinition(name, mappings) {
            this.Name = name;
            this.Mappings = mappings;
        }

        function column(field, title, width, template, editor, format, visible, editable) {
            this.field = field;
            this.title = title;
            this.width = width;
            if (typeof template !== 'undefined' && template)
                this.template = template;
            if (typeof editor !== 'undefined' && editor)
                this.editor = editor;
            if (typeof format !== 'undefined' && format)
                this.format = format;
            if (typeof visible !== 'undefined' && visible)
                this.visible = visible;
            if (typeof editable !== 'undefined' && editable)
                this.editable = editable;
        }

        function logonCredential(Username, Password, Anonymous) {
            this.Username = Username;
            this.Password = Password;
            this.Anonymous = Anonymous
        }

        function saveServer(e) {
            var grid = $('#Servers').data("kendoGrid");
            setTimeout(function () {
                rawdata.Servers = cleanupClientId(grid.dataSource.data(), grid.columns);
                saveDataToTextBox();
            }, 500);
        }

        function removeServer(e) {
            var grid = $('#Servers').data("kendoGrid");
            setTimeout(function () {
                rawdata.Servers = cleanupClientId(grid.dataSource.data(), grid.columns);
                saveDataToTextBox();
            }, 500);
        }

        function saveSyncTree(e) {
            var array = [];
            for (var i = 0; i < $('.ad-exception-textbox').length; i++) {
                var val = $('.ad-exception-textbox').eq(i).val();
                if (val !== '')
                    array.push(val);
            }
            e.model.set("ADExceptions", array);
            var mapping = $('.sn-mapping-dropdown').find(":selected").val();
            e.model.set("Mappings", mapping);
            var server = $('.sn-server-dropdown').find(":selected").val();
            e.model.set("Server", server);

            rawdata.SyncTrees = $('#SyncTrees').data("kendoGrid").dataSource.data();
            saveDataToTextBox();
        }

        function removeSyncTree() {
            rawdata.SyncTrees = $('#SyncTrees').data("kendoGrid").dataSource.data();
            saveDataToTextBox();
        }

        function saveADPropery(e) {
            var currentText = $(e.sender.element).closest('.sn-propertymapping').children('h3').text();
            var textArray = currentText.split('(');
            $(e.sender.element).closest('.sn-propertymapping').children('h3').text(textArray[0] + '(' + getPropertyNames(e.sender._data) + ')');
            var containerName = e.sender.element.closest('.sn-adsync-mapping-listitem').find('#textbox-Name').val();
            for (var i = 0; i < rawdata.MappingDefinitions.length ; i++) {
                if (rawdata.MappingDefinitions[i].Name == containerName) {
                    var saveableData = $('#' + e.sender.element.attr('id')).data("kendoGrid").dataSource.data();
                    var index = getClosestPropertyMappingIndex($('#' + e.sender.element.attr('id')));
                    var grid = $('#' + e.sender.element.attr('id')).data("kendoGrid");
                    if (saveableData.length > 0)
                        rawdata.MappingDefinitions[i].Mappings[index - 1].ADProperties = removeId($('#' + e.sender.element.attr('id')).data("kendoGrid").dataSource.data(), grid.columns);
                }
            }
            saveDataToTextBox();
        }

        function removeAdProperty() {

        }

        function savePortalProperty(e) {
            var containerName = e.sender.element.closest('.sn-adsync-mapping-listitem').find('#textbox-Name').val();
            for (var i = 0; i < rawdata.MappingDefinitions.length ; i++) {
                if (rawdata.MappingDefinitions[i].Name == containerName) {
                    var saveableData = $('#' + e.sender.element.attr('id')).data("kendoGrid").dataSource.data();
                    var index = getClosestPropertyMappingIndex($('#' + e.sender.element.attr('id')));
                    var grid = $('#' + e.sender.element.attr('id')).data("kendoGrid");
                    if (saveableData.length > 0) {
                        rawdata.MappingDefinitions[i].Mappings[index - 1].PortalProperties = removeId(grid.dataSource.data(), grid.columns);
                    }
                }
            }
            saveDataToTextBox();
        }

        function removePortalProperty() { }

        function setBooleanValue(d, key, value) {
            d[key] = value;
            saveDataToTextBox();
        }

        function setStringValue(d, key, value) {
            d[key] = value;
            saveDataToTextBox();
        }

        function setNumberValue(d, key, value) {
            d[key] = value;
            saveDataToTextBox();
        }

        function saveSeparator(d, key, value, $input) {
            var mappingIndex = $input.closest('.sn-adsync-mapping-listitem').attr('id').split('-')[$input.closest('.sn-mapping-grid-container').attr('id').split('-').length - 1];
            var index = $input.closest('.sn-mapping-grid-container').attr('id').split('-')[$input.closest('.sn-mapping-grid-container').attr('id').split('-').length - 1];
            rawdata.MappingDefinitions[mappingIndex - 1].Mappings[index - 1][key] = value;
            saveDataToTextBox();
        }

        function setDefaultValue(d, defaults) {
            for (var i = 0; i < d.length; i++) {
                for (var j = 0; j < defaults.length; j++) {
                    $.each(defaults[j], function (key, value) {
                        if (typeof d[i][key] === 'undefined')
                            d[i][key] = value;
                    });
                }
            }
            return d;
        }

        function buildEnablingInfo($input) {
            var $enabledInfoRow = $('<div class="sn-info">' + SN.Resources.ADSync["AdSyncIsDisabled"] + '</div>').appendTo($input.parent());
            if ($input.is(":checked"))
                $enabledInfoRow.addClass('hidden');
            $input.on('change', function () {
                if ($input.is(":checked"))
                    $enabledInfoRow.addClass('hidden');
                else
                    $enabledInfoRow.removeClass('hidden');
            });
        }

        function getClosestPropertyMappingIndex($el) {
            return $el.closest('.sn-propertymapping').index();
        }

        function saveDataToTextBox() {
            setTimeout(function () {
                aDSyncSettingsEditor.$el.val(JSON.stringify(rawdata, null, "\t"));
            }, 200);
        }

        function IsJsonString(str) {
            try {
                JSON.parse(str);
            } catch (err) {
                return false;
            }
            return true;
        }

        function createGuid() {
            function s4() {
                return Math.floor((1 + Math.random()) * 0x10000)
                  .toString(16)
                  .substring(1);
            }
            return '*' + s4() + s4() + '-' + s4() + '-' + s4() + '-' +
              s4() + '-' + s4() + s4() + s4();
        }

        function removeId(array, columns) {
            var columnNameArray = getColumns(columns);
            var newArray = [];
            var obj;
            for (var i = 0; i < array.length; i++) {
                obj = {};
                $.each(array[i], function (key, value) {
                    if (columnNameArray.indexOf(key) > -1 && key !== 'Id' && key !== 'id') {
                        obj[key] = value;
                    }
                });
                newArray.push(obj);
            }
            return newArray;
        }

        function cleanupClientId(array, columns) {
            var columnNameArray = getColumns(columns);
            var newArray = [];
            var obj;
            for (var i = 0; i < array.length; i++) {
                obj = {};
                $.each(array[i], function (key, value) {
                    if (columnNameArray.indexOf(key) > -1) {
                        // clean only client-side generared ids that start with '*' (see createGuid method)
                        if ((key !== 'Id' && key !== 'id') || value.toString().substr(0, 1) !== '*') {
                            obj[key] = value;
                        } else {
                            obj[key] = '';
                        }
                    }
                });
                newArray.push(obj);
            }
            return newArray;
        }

        function getColumns(columns) {
            var columnNames = [];
            for (var i = 0; i < columns.length; i++) {
                if (typeof columns[i].field !== 'undefined')
                    columnNames.push(columns[i].field);
            }
            return columnNames;
        }

        //templates
        aDSyncSettingsEditor.credentialsTemplate = '<ul>#if(LogonCredentials.Anonymous === false){#<li>Username: #=LogonCredentials.Username#</li>#}#\
                                   <li>Anonymous: <span class="fa sn-icon sn-icon-inline sn-icon-#=LogonCredentials.Anonymous#">#=LogonCredentials.Anonymous#</span></li></ul>';

        aDSyncSettingsEditor.BooleanTemplate = function (field) {
            return '<span class="fa sn-icon sn-icon-#=' + field + '#">#=' + field + '#</span>';
        }

        aDSyncSettingsEditor.serverDetailTemplate = '<div><div class="server-details">\
                            <ul>\
                                <li><label>' + SN.Resources.ADSync["UseSsl-DisplayName"] + ': </label><span class="fa sn-icon sn-icon-#=UseSsl#">#= UseSsl #</span></li>\
                                <li><label>' + SN.Resources.ADSync["Port-DisplayName"] + ': </label>#= Port #</li>\
                            </ul>\
                        </div>\
                    </div>';
        aDSyncSettingsEditor.syncTreeDetailTemplate = '<div><div class="syncTree-details">\
                            <ul>\
                                <li><label>' + SN.Resources.ADSync["UserFilter-DisplayName"] + ': </label>#= UserFilter #</li>\
                                <li><label>' + SN.Resources.ADSync["GroupFilter-DisplayName"] + ': </label>#= GroupFilter #</li>\
                                <li><label>' + SN.Resources.ADSync["ContainerFilter-DisplayName"] + ': </label>#= ContainerFilter #</li>\
                            </ul>\
                        </div>\
                    </div>';

        aDSyncSettingsEditor.aDExceptionsTemplate = function (dataItem) {
            var $html = $('<ul></ul>');

            for (var i = 0; i < dataItem.ADExceptions.length; i++) {
                $html.append('<li>' + dataItem.ADExceptions[i] + '</li>');
            }

            return $html.prop('outerHTML');
        }

        //editors
        aDSyncSettingsEditor.credentialEditor = function (container, options) {
            $.each(options.model.LogonCredentials, function (key, value) {
                if (typeof options.model.LogonCredentials[key] === 'boolean' && key !== 'uid') {
                    SN.Controls.Boolean.render(container, {
                        label: SN.Resources.ADSync[key + "-DisplayName"],
                        value: options.model.LogonCredentials[key],
                        key: 'LogonCredentials.' + key,
                        databind: 'checked:LogonCredentials.' + key
                    });
                }
                else if (typeof options.model.LogonCredentials[key] === 'number' && key !== 'uid') {
                    SN.Controls.Number.render(container, {
                        label: SN.Resources.ADSync[key + "-DisplayName"],
                        value: options.model.LogonCredentials[key],
                        key: 'LogonCredentials.' + key,
                        databind: 'value:LogonCredentials.' + key
                    });
                }
                else if (typeof options.model.LogonCredentials[key] === 'string' && key !== 'uid') {
                    if (key === 'Password')
                        SN.Controls.Password.render(container, {
                            label: SN.Resources.ADSync[key + "-DisplayName"],
                            value: options.model.LogonCredentials[key],
                            key: 'LogonCredentials.' + key,
                            databind: 'value:LogonCredentials.' + key
                        });

                    else
                        SN.Controls.String.render(container, {
                            label: SN.Resources.ADSync[key + "-DisplayName"],
                            value: options.model.LogonCredentials[key],
                            key: 'LogonCredentials.' + key,
                            databind: 'value:LogonCredentials.' + key
                        });
                }
            });

            var desc = options.field + '-Description';
            SN.Controls.Information.render(SN.Resources.ADSync[desc], container);

            if (options.model.LogonCredentials["Anonymous"])
                container.find('.sn-formrow:not(:eq(-1))').hide();

            container.find('input[type="checkbox"]').on('click', function () {
                var $this = $(this);
                if ($this.prop("checked"))
                    container.find('.sn-formrow:not(:eq(-1))').hide();
                else
                    container.find('.sn-formrow:not(:eq(-1))').show();
            });
        };
        aDSyncSettingsEditor.aDExceptionsEditor = function (container, options) {
            $.each(options.model.ADExceptions, function (key, value) {
                SN.Controls.String.render(container, {
                    label: SN.Resources.ADSync["Exception"] + (key + 1),
                    value: options.model.ADExceptions[key],
                    extraClass: 'ad-exception-textbox'
                });
            });
            var num = 1;
            SN.Controls.String.render(container, {
                label: SN.Resources.ADSync["Exception"] + (options.model.ADExceptions.length + num),
                value: '',
                extraClass: 'ad-exception-textbox'
            });
            var $addButton = $('<a class="k-button k-button-icontext k-grid-add" href="#"><span class="k-icon k-add"></span>' + SN.Resources.ADSync["AddNewException"] + '</a>').appendTo(container);
            num += 1;
            var desc = options.field + '-Description';
            SN.Controls.Information.render(SN.Resources.ADSync[desc], container);

            $addButton.on('click', function (e) {
                e.preventDefault();
                SN.Controls.String.render(container, {
                    label: SN.Resources.ADSync["Exception"] + (options.model.ADExceptions.length + num),
                    value: '',
                    extraClass: 'ad-exception-textbox'
                });
                num += 1;
                $addButton.appendTo(container);
            });
        };
        aDSyncSettingsEditor.mappingsEditor = function (container, options) {
            var $html = $('<select class="sn-mapping-dropdown"></select>');
            for (var i = 0; i < data.MappingDefinitions.length; i++) {
                var $option = $('<option value="' + data.MappingDefinitions[i].Name + '">' + data.MappingDefinitions[i].Name + '</option>').appendTo($html);
                if (data.MappingDefinitions[i].Name === options.model.Mappings)
                    $option.attr('selected', true)
            }
            container.append($html);

        };
        aDSyncSettingsEditor.serverEditor = function (container, options) {
            var $html = $('<select class="sn-server-dropdown"></select>');
            for (var i = 0; i < rawdata.Servers.length; i++) {
                var $option = $('<option value="' + rawdata.Servers[i].Name + '">' + rawdata.Servers[i].Name + '</option>').appendTo($html);
                if (rawdata.Servers[i].Name === options.model.Server)
                    $option.attr('selected', true)
            }
            container.append($html);
        };

        aDSyncSettingsEditor.StringEditor = function (container, options) {

            var grid = $('tr[data-uid="' + options.model.uid + '"').closest('.sn-grid').data("kendoGrid");
            var validation = grid.options.dataSource.schema.model.fields[options.field].validation;
            if (typeof validation !== 'undefined')
                var required = grid.options.dataSource.schema.model.fields[options.field].validation.required;

            var $input = $('<input class="k-input k-textbox" id="' + options.field + '" type="text" name="' + options.field + '" data-bind="value:' + options.field + '"></input>');

            if (typeof required !== 'undefined' && required) {
                $input.attr('required', 'required');
                container.prev('.k-edit-label').find('label').addClass('km-required');
            }

            $input.val(options.model[options.field]);
            container.append($input);
            var desc = options.field + '-Description';
            if (typeof SN.Resources.ADSync[desc] !== 'undefined')
                SN.Controls.Information.render(SN.Resources.ADSync[desc], container);
        }
        aDSyncSettingsEditor.BooleanEditor = function (container, options) {
            var $input = $('<input id="' + options.field + '" type="checkbox" name="' + options.field + '" data-type="boolean" data-bind="checked:' + options.field + '"></input>');
            if (options.model[options.field])
                $input.prop('checked', true);
            container.append($input);
            var desc = options.field + '-Description';
            if (typeof SN.Resources.ADSync[desc] !== 'undefined')
                SN.Controls.Information.render(SN.Resources.ADSync[desc], container);
        }
        aDSyncSettingsEditor.NumberEditor = function (container, options) {
            var $input = $('<input id="' + options.field + '" type="text" name="' + options.field + '" data-bind="value:' + options.field + '"></input>');
            $input.val(options.model[options.field]);
            container.append($input);
            $input.kendoNumericTextBox({
                format: "#",
                decimals: 0
            });
        }
     

        function fieldsAreValid() {
            return $('input[required]:text[value=""]').length === 0;
        }
    }
    $.ADSyncSettingsEditor.defaultOptions = {
    };
    $.fn.ADSyncSettingsEditor = function (options) {
        return this.each(function () {
            var aDSyncSettingsEditor = new $.ADSyncSettingsEditor(this, options);
            aDSyncSettingsEditor.init();
        });
    };
})(jQuery);