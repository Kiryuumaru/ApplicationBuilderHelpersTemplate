// Blazor JS initializer - automatically loaded by Blazor runtime
// No manual script tag needed in App.razor

export function beforeWebStart() {
    // Register IndexedDB LocalStore API before Blazor starts
    window.indexedDBLocalStore = {
        /**
         * Opens the IndexedDB database, creating it if necessary.
         * @param {string} dbName - Database name
         * @param {string} storeName - Object store name
         * @param {number} version - Database version
         * @returns {Promise<IDBDatabase>} The opened database
         */
        openDatabase: function (dbName, storeName, version) {
            return new Promise((resolve, reject) => {
                const request = indexedDB.open(dbName, version);

                request.onerror = () => {
                    reject(new Error(`Failed to open IndexedDB: ${request.error?.message || 'Unknown error'}`));
                };

                request.onsuccess = () => {
                    resolve(request.result);
                };

                request.onupgradeneeded = (event) => {
                    const db = event.target.result;
                    if (!db.objectStoreNames.contains(storeName)) {
                        const store = db.createObjectStore(storeName, { keyPath: ['group', 'id'] });
                        store.createIndex('group', 'group', { unique: false });
                    }
                };
            });
        },

        /**
         * Gets a value from the store.
         * @param {string} dbName - Database name
         * @param {string} storeName - Object store name
         * @param {number} version - Database version
         * @param {string} group - Group key
         * @param {string} id - Item ID
         * @returns {Promise<string|null>} The stored data or null
         */
        get: async function (dbName, storeName, version, group, id) {
            const db = await this.openDatabase(dbName, storeName, version);
            try {
                return await new Promise((resolve, reject) => {
                    const transaction = db.transaction(storeName, 'readonly');
                    const store = transaction.objectStore(storeName);
                    const request = store.get([group, id]);

                    request.onerror = () => reject(request.error);
                    request.onsuccess = () => resolve(request.result?.data ?? null);
                });
            } finally {
                db.close();
            }
        },

        /**
         * Gets all IDs for a group.
         * @param {string} dbName - Database name
         * @param {string} storeName - Object store name
         * @param {number} version - Database version
         * @param {string} group - Group key
         * @returns {Promise<string[]>} Array of IDs
         */
        getIds: async function (dbName, storeName, version, group) {
            const db = await this.openDatabase(dbName, storeName, version);
            try {
                return await new Promise((resolve, reject) => {
                    const transaction = db.transaction(storeName, 'readonly');
                    const store = transaction.objectStore(storeName);
                    const index = store.index('group');
                    const request = index.getAll(IDBKeyRange.only(group));

                    request.onerror = () => reject(request.error);
                    request.onsuccess = () => {
                        const ids = request.result.map(item => item.id);
                        resolve(ids);
                    };
                });
            } finally {
                db.close();
            }
        },

        /**
         * Checks if an entry exists.
         * @param {string} dbName - Database name
         * @param {string} storeName - Object store name
         * @param {number} version - Database version
         * @param {string} group - Group key
         * @param {string} id - Item ID
         * @returns {Promise<boolean>} True if exists
         */
        contains: async function (dbName, storeName, version, group, id) {
            const db = await this.openDatabase(dbName, storeName, version);
            try {
                return await new Promise((resolve, reject) => {
                    const transaction = db.transaction(storeName, 'readonly');
                    const store = transaction.objectStore(storeName);
                    const request = store.count([group, id]);

                    request.onerror = () => reject(request.error);
                    request.onsuccess = () => resolve(request.result > 0);
                });
            } finally {
                db.close();
            }
        },

        /**
         * Commits a batch of operations atomically.
         * @param {string} dbName - Database name
         * @param {string} storeName - Object store name
         * @param {number} version - Database version
         * @param {Array} operations - Array of operations to commit
         */
        commitOperations: async function (dbName, storeName, version, operations) {
            if (!operations || operations.length === 0) {
                return;
            }

            const db = await this.openDatabase(dbName, storeName, version);
            try {
                await new Promise((resolve, reject) => {
                    const transaction = db.transaction(storeName, 'readwrite');
                    const store = transaction.objectStore(storeName);

                    transaction.onerror = () => reject(transaction.error);
                    transaction.oncomplete = () => resolve();

                    for (const op of operations) {
                        if (op.type === 'set') {
                            store.put({
                                group: op.group,
                                id: op.id,
                                data: op.data
                            });
                        } else if (op.type === 'delete') {
                            store.delete([op.group, op.id]);
                        }
                    }
                });
            } finally {
                db.close();
            }
        }
    };
}
