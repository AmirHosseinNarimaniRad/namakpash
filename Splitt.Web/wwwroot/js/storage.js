// The database file lives in OPFS; SQLite itself runs against Emscripten's in-memory filesystem.
//
// Nothing syncs those two automatically. On startup the bytes are read out of OPFS and handed to
// .NET, which writes them into the virtual filesystem before opening the connection; after every
// write the whole file comes back and is rewritten here. The database is a few tens of KB for a
// realistic trip, so whole-file writes cost less than the machinery needed to avoid them.
//
// sqlite-net leaves SQLite in its default DELETE journal mode, so the main file is complete and
// consistent the moment a transaction commits. That is what makes copying it out safe.

const FILE = "namakpash.db3";

async function root() {
  if (!navigator.storage || !navigator.storage.getDirectory) {
    throw new Error("OPFS is not available in this browser");
  }
  return await navigator.storage.getDirectory();
}

export async function load() {
  try {
    const dir = await root();
    const handle = await dir.getFileHandle(FILE);
    const file = await handle.getFile();
    const buffer = await file.arrayBuffer();
    return new Uint8Array(buffer);
  } catch (err) {
    // NotFoundError simply means this device has no database yet — a first run, not a failure.
    if (err && err.name === "NotFoundError") return null;
    throw err;
  }
}

export async function save(bytes) {
  const dir = await root();
  // Write to a temporary name and swap it in, so a tab closed mid-write cannot leave a
  // half-written database where the real one used to be.
  const tempName = FILE + ".writing";
  const temp = await dir.getFileHandle(tempName, { create: true });
  const stream = await temp.createWritable();
  await stream.write(bytes);
  await stream.close();

  const target = await dir.getFileHandle(FILE, { create: true });
  const swap = await target.createWritable();
  await swap.write(await (await temp.getFile()).arrayBuffer());
  await swap.close();
  await dir.removeEntry(tempName).catch(function () { /* best effort */ });

  return bytes.length;
}

export async function requestPersist() {
  try {
    if (!navigator.storage || !navigator.storage.persist) return false;
    if (await navigator.storage.persisted()) return true;
    return await navigator.storage.persist();
  } catch (err) {
    return false;
  }
}

export async function estimate() {
  try {
    if (!navigator.storage || !navigator.storage.estimate) return null;
    const est = await navigator.storage.estimate();
    return { quota: est.quota || 0, usage: est.usage || 0 };
  } catch (err) {
    return null;
  }
}

export function isStandalone() {
  return (window.matchMedia && window.matchMedia("(display-mode: standalone)").matches)
    || window.navigator.standalone === true;
}

// Downloading a backup: the app hands over JSON, the browser saves it as a file.
export function downloadText(filename, text) {
  const blob = new Blob([text], { type: "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
}

// Restoring one: a file input the app opens on demand, resolved as text.
export function pickTextFile() {
  return new Promise(function (resolve) {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "application/json,.json";
    input.style.display = "none";
    document.body.appendChild(input);

    input.addEventListener("change", async function () {
      const file = input.files && input.files[0];
      document.body.removeChild(input);
      if (!file) { resolve(null); return; }
      resolve(await file.text());
    });

    // A cancelled picker fires no event in most browsers, so nothing resolves and the caller
    // simply never continues — which is the correct outcome for "the user changed their mind".
    input.click();
  });
}

// The report is a full A4 document built by Splitt.Core, so it is printed on its own rather
// than through the app's page: an iframe gets its own print context, which keeps the app's
// layout and stylesheet out of the output entirely.
export function printHtml(html) {
  const frame = document.createElement("iframe");
  frame.setAttribute("aria-hidden", "true");
  frame.style.cssText = "position:fixed;inset-inline-start:-10000px;top:0;width:794px;height:1123px;border:0";
  document.body.appendChild(frame);

  const doc = frame.contentDocument;
  doc.open();
  doc.write(html);
  doc.close();

  // Fonts load asynchronously; printing before they arrive lays the report out in a fallback
  // face and the Persian text reflows.
  const go = function () {
    frame.contentWindow.focus();
    frame.contentWindow.print();
    setTimeout(function () { document.body.removeChild(frame); }, 1000);
  };

  if (doc.fonts && doc.fonts.ready) {
    doc.fonts.ready.then(function () { setTimeout(go, 120); });
  } else {
    setTimeout(go, 400);
  }
}

// Used only by the self-test route when it is opened with ?report=1, so an automated run can
// collect the result instead of a human reading the screen. Failure is ignored: in production
// there is nothing listening, and that is fine.
export async function postJson(url, text) {
  try {
    await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: text });
    return true;
  } catch (err) {
    return false;
  }
}
