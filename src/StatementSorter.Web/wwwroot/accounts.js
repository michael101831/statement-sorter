import { firebaseConfig } from "./firebase-config.js";

const bridge = window.statementSorterPersonal;
const area = document.querySelector("#account-area");
const backdrop = document.querySelector("#account-backdrop");
const form = document.querySelector("#account-form");
const title = document.querySelector("#account-title");
const description = document.querySelector("#account-desc");
const error = document.querySelector("#account-modal-error");
const nameField = document.querySelector("#account-name-field");
const nameInput = document.querySelector("#account-name");
const emailInput = document.querySelector("#account-email");
const passwordInput = document.querySelector("#account-password");
const confirmButton = document.querySelector("#account-confirm");
const toggleButton = document.querySelector("#account-toggle");

let authMode = "login";
let currentUser = null;
let auth = null;
let db = null;
let authApi = null;
let firestoreApi = null;

const signedInCopy = "Sign in to keep your caps, one-offs, and savings goals with this account.";
const registerCopy = "Create an account to keep your caps, one-offs, and savings goals with this account.";

const firebaseReady = (async () => {
  const appModule = await import("https://www.gstatic.com/firebasejs/10.13.0/firebase-app.js");
  authApi = await import("https://www.gstatic.com/firebasejs/10.13.0/firebase-auth.js");
  firestoreApi = await import("https://www.gstatic.com/firebasejs/10.13.0/firebase-firestore.js");
  const firebaseApp = appModule.initializeApp(firebaseConfig);
  auth = authApi.getAuth(firebaseApp);
  db = firestoreApi.getFirestore(firebaseApp);
  authApi.onAuthStateChanged(auth, async (user) => {
    if (user) await loadUserState(user);
    else clearUserState();
    renderAccount();
  });
})().catch(() => {
  auth = null;
});

bridge.accountBridge.signedIn = () => currentUser !== null;
bridge.accountBridge.scheduleSave = () => {
  clearTimeout(bridge.accountBridge.timer);
  bridge.accountBridge.timer = setTimeout(() => {
    savePersonal().catch(() => {
      const note = document.querySelector("#account-save-error");
      if (!note) return;
      note.hidden = false;
      note.textContent = "Couldn't save those notes to the account. Try again in a moment.";
    });
  }, 250);
};

function friendlyAuthError(err) {
  const code = err && err.code;
  const map = {
    "auth/email-already-in-use": "An account with that email already exists.",
    "auth/invalid-email": "Enter a valid email address.",
    "auth/weak-password": "Password must be at least 6 characters.",
    "auth/wrong-password": "Incorrect email or password.",
    "auth/user-not-found": "Incorrect email or password.",
    "auth/invalid-credential": "Incorrect email or password.",
    "auth/too-many-requests": "Too many attempts — please wait a moment and try again.",
    "auth/operation-not-allowed": "Email sign-in isn't turned on for this project yet.",
    "auth/network-request-failed": "Couldn't reach the account service. Check the connection and try again.",
  };
  return map[code] || (err && err.message) || "Something went wrong. Please try again.";
}

function showModalError(message) {
  error.hidden = false;
  error.textContent = message;
}

async function loadUserState(user) {
  currentUser = user;
  const snap = await firestoreApi.getDoc(firestoreApi.doc(db, "users", user.uid));
  const data = snap.exists() ? snap.data() : null;
  if (!data || !Array.isArray(data.goals)) {
    await firestoreApi.setDoc(
      firestoreApi.doc(db, "users", user.uid),
      {
        ...bridge.readPersonal(),
        displayName: user.displayName || "",
        createdAt: new Date().toISOString(),
      },
      { merge: true },
    );
    return;
  }
  bridge.applyPersonal(data);
}

function clearUserState() {
  const wasSignedIn = currentUser !== null;
  currentUser = null;
  if (wasSignedIn) bridge.reloadLocalPersonal();
}

async function savePersonal() {
  if (!currentUser || !db) return;
  const note = document.querySelector("#account-save-error");
  await firestoreApi.setDoc(firestoreApi.doc(db, "users", currentUser.uid), bridge.readPersonal(), { merge: true });
  if (note) note.hidden = true;
}

async function registerAccount(email, password, displayName) {
  const cred = await authApi.createUserWithEmailAndPassword(auth, email, password);
  await authApi.updateProfile(cred.user, { displayName });
  await firestoreApi.setDoc(firestoreApi.doc(db, "users", cred.user.uid), {
    ...bridge.readPersonal(),
    displayName,
    createdAt: new Date().toISOString(),
  });
}

function setModalMode(mode) {
  authMode = mode;
  const isRegister = mode === "register";
  title.textContent = isRegister ? "Create account" : "Sign in";
  description.textContent = isRegister ? registerCopy : signedInCopy;
  nameField.hidden = !isRegister;
  confirmButton.textContent = isRegister ? "Create account" : "Sign in";
  toggleButton.textContent = isRegister ? "Have an account?" : "Need an account?";
  passwordInput.autocomplete = isRegister ? "new-password" : "current-password";
  error.hidden = true;
}

function openModal(mode) {
  setModalMode(mode || "login");
  backdrop.classList.add("open");
  emailInput.value = "";
  passwordInput.value = "";
  nameInput.value = "";
  emailInput.focus();
}

function closeModal() {
  backdrop.classList.remove("open");
}

function renderAccount() {
  area.replaceChildren();
  if (currentUser) {
    const chip = document.createElement("span");
    chip.className = "signed-in-chip";
    chip.textContent = `@ ${currentUser.displayName || currentUser.email}`;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "ghost";
    button.textContent = "Sign out";
    button.addEventListener("click", async () => {
      clearTimeout(bridge.accountBridge.timer);
      try {
        await savePersonal();
      } catch {
        // Sign-out still returns to the notes kept in this browser.
      }
      await authApi.signOut(auth);
    });
    const note = document.createElement("p");
    note.id = "account-save-error";
    note.className = "error account-save-error";
    note.hidden = true;
    area.append(chip, button, note);
    return;
  }

  const button = document.createElement("button");
  button.type = "button";
  button.className = "primary";
  button.textContent = "Sign in";
  button.addEventListener("click", () => openModal("login"));
  area.append(button);
}

renderAccount();

toggleButton.addEventListener("click", () => {
  setModalMode(authMode === "login" ? "register" : "login");
});

document.querySelector("#account-cancel").addEventListener("click", closeModal);
backdrop.addEventListener("click", (event) => {
  if (event.target === backdrop) closeModal();
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && backdrop.classList.contains("open")) closeModal();
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const email = emailInput.value.trim();
  const password = passwordInput.value;
  const displayName = nameInput.value.trim();
  confirmButton.disabled = true;
  error.hidden = true;

  try {
    await firebaseReady;
    if (!auth || !db) throw new Error("Couldn't reach the account service. Check the connection and try again.");
    if (authMode === "register") {
      if (!displayName) throw new Error("Enter a display name.");
      await registerAccount(email, password, displayName);
    } else {
      await authApi.signInWithEmailAndPassword(auth, email, password);
    }
    closeModal();
  } catch (err) {
    showModalError(friendlyAuthError(err));
  } finally {
    confirmButton.disabled = false;
  }
});
