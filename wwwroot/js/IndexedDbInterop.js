//A minimal string key-value store over IndexedDB, backing IndexedDbLocalStorage (an ILocalStorageService
//that uses IndexedDB in place of localStorage). IndexedDB's per-origin quota is far larger than
//localStorage's ~5 MB, so a big saved app-state blob no longer hits the wall. The database name is per-app
//so the several Decent apps that share the eecs.blog origin keep separate stores. Values are opaque strings
//(the C# layer handles JSON + compression). Writes never throw: set() resolves false on a full quota or
//when storage is unavailable, so a failed save can be surfaced instead of crashing.
window.appIdb = {
    _dbName: 'CsharpOnlineCompiler',
    _store: 'kv',
    _dbPromise: null,

    _open: function () {
        if (this._dbPromise)
            return this._dbPromise;

        var self = this;
        this._dbPromise = new Promise(function (resolve, reject) {
            var req;

            try {
                req = indexedDB.open(self._dbName, 1);
            }
            catch (e) {
                reject(e);
                return;
            }

            req.onupgradeneeded = function () {
                req.result.createObjectStore(self._store);
            };
            req.onsuccess = function () {
                resolve(req.result);
            };
            req.onerror = function () {
                reject(req.error);
            };
        });

        return this._dbPromise;
    },

    //Reads the string stored under key, or null when absent / on any failure.
    get: async function (key) {
        try {
            var db = await this._open();
            var store = this._store;

            return await new Promise(function (resolve, reject) {
                var tx = db.transaction(store, 'readonly');
                var req = tx.objectStore(store).get(key);
                req.onsuccess = function () {
                    resolve(req.result === undefined ? null : req.result);
                };
                req.onerror = function () {
                    reject(req.error);
                };
            });
        }
        catch (e) {
            return null;
        }
    },

    //Writes value under key. Resolves true on success, false on a full quota / disabled storage — never throws.
    set: async function (key, value) {
        try {
            var db = await this._open();
            var store = this._store;

            return await new Promise(function (resolve) {
                var tx;

                try {
                    tx = db.transaction(store, 'readwrite');
                }
                catch (e) {
                    resolve(false);
                    return;
                }

                tx.objectStore(store).put(value, key);
                tx.oncomplete = function () {
                    resolve(true);
                };
                tx.onerror = function () {
                    resolve(false);
                };
                tx.onabort = function () {
                    resolve(false);
                };
            });
        }
        catch (e) {
            return false;
        }
    },

    remove: async function (key) {
        try {
            var db = await this._open();
            var store = this._store;

            await new Promise(function (resolve) {
                var tx = db.transaction(store, 'readwrite');
                tx.objectStore(store).delete(key);
                tx.oncomplete = function () {
                    resolve();
                };
                tx.onerror = function () {
                    resolve();
                };
            });
        }
        catch (e) { }
    },

    //Returns every key in the store (an empty array on failure).
    keys: async function () {
        try {
            var db = await this._open();
            var store = this._store;

            return await new Promise(function (resolve) {
                var tx = db.transaction(store, 'readonly');
                var req = tx.objectStore(store).getAllKeys();
                req.onsuccess = function () {
                    resolve((req.result || []).map(String));
                };
                req.onerror = function () {
                    resolve([]);
                };
            });
        }
        catch (e) {
            return [];
        }
    },

    clear: async function () {
        try {
            var db = await this._open();
            var store = this._store;

            await new Promise(function (resolve) {
                var tx = db.transaction(store, 'readwrite');
                tx.objectStore(store).clear();
                tx.oncomplete = function () {
                    resolve();
                };
                tx.onerror = function () {
                    resolve();
                };
            });
        }
        catch (e) { }
    },

    //Whether IndexedDB is usable at all (it's disabled in some private-browsing modes).
    available: function () {
        try {
            return typeof indexedDB !== 'undefined' && indexedDB !== null;
        }
        catch (e) {
            return false;
        }
    }
};
