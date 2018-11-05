/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

function IssuesExtension(viewer, options) {
  Autodesk.Viewing.Extension.call(this, viewer, options);
}

IssuesExtension.prototype = Object.create(Autodesk.Viewing.Extension.prototype);
IssuesExtension.prototype.constructor = IssuesExtension;

IssuesExtension.prototype.load = function () {
  if (this.viewer.toolbar) {
    // Toolbar is already available, create the UI
    this.createUI();
  } else {
    // Toolbar hasn't been created yet, wait until we get notification of its creation
    this.onToolbarCreatedBinded = this.onToolbarCreated.bind(this);
    this.viewer.addEventListener(Autodesk.Viewing.TOOLBAR_CREATED_EVENT, this.onToolbarCreatedBinded);
  }
  return true;
};

IssuesExtension.prototype.onToolbarCreated = function () {
  this.viewer.removeEventListener(Autodesk.Viewing.TOOLBAR_CREATED_EVENT, this.onToolbarCreatedBinded);
  this.onToolbarCreatedBinded = null;
  this.createUI();
};

IssuesExtension.prototype.createUI = function () {
  var _this = this;

  // prepare to execute the button action
  var issuesTollbarBtn = new Autodesk.Viewing.UI.Button('showIssues');
  issuesTollbarBtn.onClick = function (e) {
    //var ids = atob(getParameterByName('id')).split(',');
    var ids = atob(getParameterByName('id')).split(",").map(function(item) {
      return item.trim();
    });

    var matchIds = [];
    getAllLeafComponents(_this.viewer, function (dbIds) {
      _this.viewer.model.getBulkProperties(dbIds, ['externalId'],
        function (elements) {
          elements.forEach(function(ele){
            if (ids.includes(ele.externalId)){
              matchIds.push(ele.dbId);
              _this.viewer.select(matchIds);
            }
          })
        })
    })
  };
  // issuesTollbarBtn CSS class should be defined on your .css file
  // you may include icons, below is a sample class:
  issuesTollbarBtn.addClass('issuesIcon');
  issuesTollbarBtn.setToolTip('Show issues');

  // SubToolbar
  this.subToolbar = (this.viewer.toolbar.getControl("MyAppToolbar") ?
    this.viewer.toolbar.getControl("MyAppToolbar") :
    new Autodesk.Viewing.UI.ControlGroup('MyAppToolbar'));
  this.subToolbar.addControl(issuesTollbarBtn);

  this.viewer.toolbar.addControl(this.subToolbar);
};

IssuesExtension.prototype.unload = function () {
  this.viewer.toolbar.removeControl(this.subToolbar);
  return true;
};

Autodesk.Viewing.theExtensionManager.registerExtension('IssuesExtension', IssuesExtension);


function getAllLeafComponents(viewer, callback) {
  var cbCount = 0; // count pending callbacks
  var components = []; // store the results
  var tree; // the instance tree

  function getLeafComponentsRec(parent) {
    cbCount++;
    if (tree.getChildCount(parent) != 0) {
      tree.enumNodeChildren(parent, function (children) {
        getLeafComponentsRec(children);
      }, false);
    } else {
      components.push(parent);
    }
    if (--cbCount == 0) callback(components);
  }
  viewer.getObjectTree(function (objectTree) {
    tree = objectTree;
    var allLeafComponents = getLeafComponentsRec(tree.getRootId());
  });
}