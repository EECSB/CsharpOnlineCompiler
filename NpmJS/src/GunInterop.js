import Gun from 'gun';
import SEA from "gun/sea";

if (typeof Gun.SEA === 'undefined') {
    Gun.SEA = SEA;
}


const workspacesContainerName = "cshOnlCompWrks";
let sharedPrivateWorkspacesContainerName = workspacesContainerName + "_shared";

let gun;
let user;
let userPublicKey;
let gunDotNetObjRef;
let lastUpdate; //Could be replaced by hash to use less memory, but it's probably ok as the appstate string will never get that big.

window.initGun = function (ref, nodes) {
    gunDotNetObjRef = ref;

    gun = Gun({ peers: nodes, localStorage: false });
    user = gun.user();
}


window.signUp = function (alias, pass) {
    user.create(alias, pass, ({ err }) => {
        if (err) {
            alert(err);
        } else {
            signIn(alias, pass);
        }
    });
}

window.signIn = async function (alias, pass) {
    user.auth(alias, pass, (ack) => {
        if (ack.err) {
            alert(ack.err);
        } else {
            userPublicKey = user.is.pub;
            gunDotNetObjRef.invokeMethodAsync('SetUserPublicKey', userPublicKey);
        }
    });
}

window.signOut = function () {
    user.leave();
}



window.saveWorkspace = async function (workspaceName, appstate) {
    if (user == undefined)
        return false;

    if (!user.is)
        return false;


    //Save the last update.
    lastUpdate = appstate;

    //Create a container object to save the data.
    const workspace = {
        name: workspaceName,
        appstate: appstate,
        createdAt: new Date().toISOString()
    }

    const encryptedContent = await Gun.SEA.encrypt(workspace, user._.sea);

    user.get(workspacesContainerName).get(workspace.name).put(encryptedContent, (ack) => {
        if (ack.err)
            alert(ack.err);
        else
            console.log('Data saved successfully');
    });
}

window.deleteWorkspace = function (workspaceName) {
    user.get(workspacesContainerName).get(workspaceName).put(null, (ack) => {
        if (ack.err)
            alert(ack.err);
        else
            console.log('Data saved successfully');
    });
}

window.sharePrivate = async function (workspaceName, shareKey, appstate) {
    if (user == undefined)
        return false;

    if (!user.is)
        return false;

    const workspace = {
        name: workspaceName,
        appstate: appstate,
        createdAt: new Date().toISOString()
    }

    const encryptedContent = await Gun.SEA.encrypt(workspace, shareKey);

    user.get(sharedPrivateWorkspacesContainerName).get(workspace.name).put(encryptedContent, (ack) => {
        if (ack.err)
            alert(ack.err);
        else
            console.log('Data shared successfully');
    });
}

window.loadPrivate = function (publicID, userPubKey, shareKey) {
    gun.user(userPubKey).get(sharedPrivateWorkspacesContainerName).get(publicID).once(async data => {
        if (data) {
            const decryptedContent = await Gun.SEA.decrypt(data, shareKey);

            gunDotNetObjRef.invokeMethodAsync('setWorkspace', decryptedContent);
        } else {
            console.log('No data found for workspace:', publicID);
        }
    });
}

window.getWorkspaces = function () {
    user.get(workspacesContainerName).map().once((data, key) => {
        if (data) {
            gunDotNetObjRef.invokeMethodAsync('setWorkspaces', key);
        }
    });
}

window.getWorkspace = function (workspace, shareKey) {
    user.get(workspacesContainerName).get(workspace).once(async (data) => {
        if (data) {
            const decryptedContent = await Gun.SEA.decrypt(data, user._.sea);

            gunDotNetObjRef.invokeMethodAsync('setWorkspace', decryptedContent);
        } else {
            console.log('No data found for workspace:', workspace);
        }
    });
}



window.shareWorkspace = async function (workspaceName, shareKey, appstate) {

    //Save the last update.
    lastUpdate = appstate;

    //Create a container object to save the data.
    const workspace = {
        name: workspaceName,
        appstate: appstate,
        createdAt: Date.now()
    }

    const encryptedContent = await Gun.SEA.encrypt(workspace, shareKey);

    gun.get(workspace.name).get("state").put(encryptedContent, (ack) => {
        if (ack.err)
            alert(ack.err);
        else
            console.log('Data saved successfully');
    });
}

window.startStreamGun = function (workspace, shareKey) {
    gun.get(workspace).get("state").on(async (data) => {
        if (data) {
            const decryptedContent = await Gun.SEA.decrypt(data, shareKey);

            //Check if the incoming change is the result of the last update we made ...
            if (decryptedContent.appstate == lastUpdate)
                return; //... if so ignore this event.

            //Set the updated appstate.
            gunDotNetObjRef.invokeMethodAsync('setWorkspaceFromLiveShare', decryptedContent.appstate);
        } else {
            console.log('No data found for workspace:', workspace);
        }
    }, { change: true });
}

window.stopStreamGun = function (workspace) {
    gun.get(workspace).get("state").off();
}

window.generateShareKey = async function () {
    const keyPair1 = await Gun.SEA.pair();
    const keyPair2 = await Gun.SEA.pair();

    const shareKey = await Gun.SEA.secret(keyPair1, keyPair2);

    return shareKey;
}