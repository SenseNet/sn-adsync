<%@ Control Language="C#" AutoEventWireup="true" Inherits="SenseNet.Portal.UI.SingleContentView" %>
<sn:scriptrequest runat="server" id="adscynSettingsEditor" Path="$skin/scripts/sn/sn.adsyncsettingseditor.js" />
<sn:cssrequest runat="server" id="adsyncSettingsEditorCss" CssPath="$skin/styles/sn.adsyncsettingseditor.css" />
<sn:Binary runat="server" ID="ADSettingsBinary" FieldName="Binary" FrameMode="NoFrame" />


<div class="sn-panel sn-buttons">
  <sn:CommandButtons ID="CommandButtons1" runat="server"/>
</div>

 
  <script>
  	var ADSyncSettingsEditor = $('.sn-ctrl-textarea').ADSyncSettingsEditor().data('ADSyncSettingsEditor');
  </script>