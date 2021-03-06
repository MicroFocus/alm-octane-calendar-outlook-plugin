﻿// (c) Copyright 2016 Hewlett Packard Enterprise Development LP

// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.

// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,

// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Hpe.Nga.Api.UI.Core.Configuration;
using MicroFocus.Adm.Octane.Api.Core.Connector.Exceptions;
using MicroFocus.Adm.Octane.Api.Core.Entities;
using MicroFocus.Adm.Octane.Api.Core.Services;
using MicroFocus.Adm.Octane.Api.Core.Services.GroupBy;
using Microsoft.Office.Core;
using SharedCalendar.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;


namespace SharedCalendar
{
    [ComVisible(true)]
  public class NgaRibbon : Office.IRibbonExtensibility
  {
    #region Fields

    private Office.IRibbonUI ribbon;
    private ConfigurationPersistService persistService = new ConfigurationPersistService();
    private Configuration config;
    private Boolean isLoggedIn = false;

    #endregion

    #region Public Members

    public NgaRibbon()
    {
      AppDomain currentDomain = AppDomain.CurrentDomain;
      currentDomain.UnhandledException += new UnhandledExceptionEventHandler(unhandledExceptionHandler);
      Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
      persistService.ConfigurationFileName = "SharedCalendar.configuration";
      config = persistService.Load<Configuration>();
    }

    public Boolean GetIsLoggedIn(IRibbonControl control)
    {
      return isLoggedIn;
    }

    public String GetBtnConnectLable(IRibbonControl control)
    {
      if (isLoggedIn)
      {
        return "Disconnect";
      }
      else
      {
        return "Connect";
      }
    }

    public void OnLogin(Office.IRibbonControl control)
    {
      if (isLoggedIn)
      {
        // disconnect
        SettingsForm.RestConnector.Disconnect();

        isLoggedIn = false;
      }
      else
      {
        // connect
        SettingsForm form = new SettingsForm();
        String calendarName = null;
        if (config != null)
        {
          calendarName = config.CalendarName;
        }
        form.Configuration = config;
        if (form.ShowDialog() == DialogResult.OK)
        {
          config = form.Configuration;
          config.CalendarName = calendarName;

          NgaUtils.init(config.SharedSpaceId, config.WorkspaceId, config.ReleaseId);
          isLoggedIn = true;

          // select the calendar tab 
          OutlookUtils.SelectCalenderModule();
        }
      }
      if (ribbon != null)
      {
        ribbon.Invalidate();
      }
    }

    public void OnSync(Office.IRibbonControl control)
    {
      try
      {
        OutlookUtils.SelectCalenderModule();
        SyncForm form = new SyncForm();
        // get calender list and initialize the form
        ICollection<String> calendars = OutlookUtils.GetCalendarList(config.CalendarName);
        form.Init(calendars, config);
        if (form.ShowDialog() == DialogResult.OK)
        {
          config.CalendarName = form.SelectedCalendar;
          //Get by id
          Release release = NgaUtils.GetSelectedRelease(); //NgaUtils.GetReleaseById(releaseId);
          EntityListResult<Sprint> sprints = NgaUtils.GetSprintsByRelease(release.Id);
          OutlookSyncUtils.SyncSprintsToOutlook(config.CalendarName, release, sprints);

          EntityListResult<Milestone> milestones = NgaUtils.GetMilestonesByRelease(release.Id);
          OutlookSyncUtils.SyncMilestonesToOutlook(config.CalendarName, release, milestones);
          String str = String.Format("Sync completed successfully.{0}Summary : {1} sprints and {2} milestones.",
              Environment.NewLine, sprints.data.Count, milestones.data.Count);
          MessageBox.Show(str, "Sync completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
      }
      catch (ServerUnavailableException)
      {
        ShowServerIsNotAvailableMsg();
      }
      catch (Exception e)
      {
        String errorMsg = "Sync failed : " + e.Message;
        MessageBox.Show(errorMsg, "Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    public void OnMailReport(Office.IRibbonControl control)
    {
      try
      {
        //Get by id
        Release release = NgaUtils.GetSelectedRelease(); //NgaUtils.GetReleaseById(releaseId);
        GroupResult groupResult = NgaUtils.GetAllDefectWithGroupBy(release.Id);
        GroupResult usGroupResult = NgaUtils.GetAllStoriesWithGroupBy(release.Id);
        OutlookSyncUtils.getReleaseMailReport(release, groupResult, usGroupResult);
      }
      catch (ServerUnavailableException)
      {
        ShowServerIsNotAvailableMsg();
      }
      catch (Exception e)
      {
        MessageBox.Show("Failed to generate report: " + e.Message + Environment.NewLine + e.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    public Bitmap imageConnect_GetImage(IRibbonControl control)
    {
      if (isLoggedIn)
      {
        return Resources.disconnect;
      }
      return Resources.connect;
    }

    public Bitmap imageSync_GetImage(IRibbonControl control)
    {
      return Resources.sync;
    }

    public Bitmap imageReport_GetImage(IRibbonControl control)
    {
      return Resources.release_report;
    }

    #endregion

    #region IRibbonExtensibility Members

    public string GetCustomUI(string ribbonID)
    {
      return GetResourceText("SharedCalendar.NgaRibbon.xml");
    }

    #endregion

    #region Ribbon Callbacks
    //Create callback methods here. For more information about adding callback methods, visit http://go.microsoft.com/fwlink/?LinkID=271226

    public void Ribbon_Load(Office.IRibbonUI ribbonUI)
    {
      this.ribbon = ribbonUI;
    }

    #endregion

    #region Private Members

    private static string GetResourceText(string resourceName)
    {
      Assembly asm = Assembly.GetExecutingAssembly();
      string[] resourceNames = asm.GetManifestResourceNames();
      for (int i = 0; i < resourceNames.Length; ++i)
      {
        if (string.Compare(resourceName, resourceNames[i], StringComparison.OrdinalIgnoreCase) == 0)
        {
          using (StreamReader resourceReader = new StreamReader(asm.GetManifestResourceStream(resourceNames[i])))
          {
            if (resourceReader != null)
            {
              return resourceReader.ReadToEnd();
            }
          }
        }
      }
      return null;
    }
    
    private void ShowServerIsNotAvailableMsg()
    {
      MessageBox.Show("ALM Octane server is not available", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OnApplicationExit(object sender, EventArgs e)
    {
      //save last successful configuration
      persistService.Save(config);
    }

    static void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
      Exception e = (Exception)args.ExceptionObject;
      MessageBox.Show("Error: " + e.Message + Environment.NewLine + e.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    #endregion


  }
}
