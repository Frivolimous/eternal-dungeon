// Eternal Dungeon: the content Google Sheet's side of `sim pull-sheets` / `sim push-sheets` (see data/README.md).
// A web app bound to the sheet: GET returns every tab's cells, POST applies a list of edits planned by the sim
// (src/Core/Data/SheetSync.cs). It runs as the sheet's owner, so the sheet itself needn't be shared.
//
// Setup (once, and again whenever this file's VERSION changes):
//   1. In the sheet: Extensions > Apps Script. Replace everything in Code.gs with this file and save.
//   2. Deploy > New deployment > type Web app. Execute as: Me. Who has access: Anyone. Deploy, and authorize.
//   3. Put the Web app URL (ends in /exec) in tools/google-sheet.json as "webApp".
// When updating: Deploy > Manage deployments > edit (pencil) > Version: New version > Deploy, so the URL stays.

var VERSION = 1;

function doGet() {
  return respond(function () {
    var tabs = SpreadsheetApp.getActiveSpreadsheet().getSheets().map(function (sheet) {
      var cells = sheet.getDataRange().getValues().map(function (row) {
        return row.map(function (v) { return v instanceof Date ? v.toISOString() : v; });
      });
      return { name: sheet.getName(), cells: cells };
    });
    return { version: VERSION, tabs: tabs };
  });
}

function doPost(e) {
  return respond(function () {
    var ops = JSON.parse(e.postData.contents).ops;
    var book = SpreadsheetApp.getActiveSpreadsheet();
    var lock = LockService.getDocumentLock();
    lock.waitLock(30000);
    try {
      ops.forEach(function (op) { apply(book, op); });
      SpreadsheetApp.flush();
    } finally {
      lock.releaseLock();
    }
    return { version: VERSION, applied: ops.length };
  });
}

function apply(book, op) {
  if (op.op === 'addTab') {
    book.insertSheet(op.tab, book.getNumSheets());
    return;
  }
  var sheet = book.getSheetByName(op.tab);
  if (!sheet) throw new Error('no tab named ' + op.tab);
  switch (op.op) {
    case 'insertRowsAfter':
      sheet.insertRowsAfter(op.row, op.count);
      break;
    case 'deleteRows':
      // A sheet must keep at least one row that isn't frozen.
      if (sheet.getMaxRows() - op.count <= sheet.getFrozenRows()) sheet.insertRowsAfter(sheet.getMaxRows(), 1);
      sheet.deleteRows(op.row, op.count);
      break;
    case 'insertColumnsAfter':
      sheet.insertColumnsAfter(op.col, op.count);
      break;
    case 'deleteColumns':
      sheet.deleteColumns(op.col, op.count);
      break;
    case 'write':
      var height = op.values.length, width = op.values[0].length;
      grow(sheet, op.row + height - 1, op.col + width - 1);
      sheet.getRange(op.row, op.col, height, width).setValues(op.values);
      break;
    default:
      throw new Error('unknown edit ' + op.op);
  }
}

function grow(sheet, rows, cols) {
  if (sheet.getMaxRows() < rows) sheet.insertRowsAfter(sheet.getMaxRows(), rows - sheet.getMaxRows());
  if (sheet.getMaxColumns() < cols) sheet.insertColumnsAfter(sheet.getMaxColumns(), cols - sheet.getMaxColumns());
}

function respond(work) {
  var result;
  try {
    result = work();
  } catch (err) {
    result = { version: VERSION, error: String(err && err.stack || err) };
  }
  return ContentService.createTextOutput(JSON.stringify(result)).setMimeType(ContentService.MimeType.JSON);
}
