import { createApp, reactive } from './petite-vue.js?module' // 'https://unpkg.com/petite-vue?module'
import { graphics } from './graphics.js' 
                
const app = reactive({

    entityType: {},
    view: {},
    views: [],
    viewsPage: 0,
    viewSearch: '',
    indexType: {},
    indexTypePage: 0,
    indexes: [],
    indexesPage: 0,
    indexSearch: '',
    entityTypes: [],
    entityId: 0,
    values: [],
    idValues: [],
    page: 0,
    nPages: 0,
    userStarter: null,
    pwd: "",
    loginMessage: "",
    loadMessage: "",
    menu: { id: 0, name: "", menuItems: [] },
    menuHistory: [],
    row: 0,
    mode: 0,
    isAdvanced: false,
    isConfig: false,
    attributeConfig: { id: 0 },
    attributeUse: new attributeUse(),
    lookupsPage: 0,
    graphAttribute: null,
    graphData: graphics,
    nextLookups() {
        this.lookupsPage++
        this.getLookups()
    },
    previousLookups() {
        this.lookupsPage--
        this.getLookups()
    },
    get viewStart() {
        return this.viewsPage * 19
    },
    get viewEnd() {
        return this.viewStart + 19;
    },
    // methods
    logout() {
        this.loginMessage = "";
        this.userStarter = null;
        this.menuHistory = [];
        this.mode = 0;
        this.pwd = "";
    },
    tryLogin() {
        let pwd = this.pwd
        if (pwd.length == 0) {
            this.authenticated = false;
            this.loginMessage = "You must enter a password";
            return;
        }
        this.loginMessage = 'Checking password..'
        fetchWrapper
            .post('login', { password: this.pwd })
            .then((values) => {
                if (values == null) {
                    this.loginMessage = 'Invalid password'
                } else {
                    this.loginMessage = "Login succeeded. Loading..";
                    this.userStarter = values.userStarter
                    fetchWrapper.token = values.token
                    this.getEntityTypes();
                    resetLoginTimer()
                }
            })
            .catch((error) => console.error(error));

    },
    getEntityTypes() {
        fetchWrapper
            .get('EntityTypes')
            .then(data => {
                this.entityTypes = data
                this.setEntityType(this.entityTypes.find(e => e.id == this.userStarter.entityTypeId))
                this.goHome()

            })
            .catch((error) => console.log('error1' + error));
    },
    getLookups() {
        fetchWrapper
            .get('Lookups/' + this.attributeConfig.id + ',' + this.lookupsPage + ',20')
            .then(data => {
                this.attributeConfig.lookups = data
            })
            .catch((error) => console.log('error1' + error));
    },
    setEntityType(entityType) {
        if (entityType !== this.entityType) {
            this.entityType = entityType
            this.setIndexType(this.entityType.indexTypes.find(t => t.id == this.entityType.defaultIndexTypeId))
            this.viewsPage = 0;
            this.indexesPage = 0;
            this.indexTypePage = 0;
            this.viewSearch = '';
            this.indexSearch = '';
            this.clear()
            this.mode = 1
            this.getIndexes()
            this.getViews()
        }
    },
    setIndexType(indexType) {
        this.indexType = indexType
    },
    async getIndexes() {
        if (this.indexSearch?.length >= 3) {
            await fetchWrapper
                .get('Indexes/' + this.indexType.id + ',' + this.indexesPage + ',19' + '/' + this.indexSearch)
                .then((data) => {
                    this.indexes = data
                })
                .catch((error) => console.error(error));
            if (indexTimer) {
                clearTimeout(indexTimer)
            }
            indexTimer = setTimeout(() => {
                this.indexesPage = 0;
                this.indexSearch = ''
                this.indexes = []
            }, 100000);

        } else this.indexes = []
    },
    async nextIndex() {
        let index = this.indexes.find(i => i.entityId == this.entityId)
        let ix = this.indexes.indexOf(index)

        if (ix < this.indexes.length - 1) {
            let newIndex = this.indexes[ix + 1]
            this.setEntity(newIndex.entityId)
        } else if (ix == 18) {
            await this.getIndexNextPage()
            if (this.indexes.some(Boolean)) {
                this.setEntity(this.indexes[0].entityId)
            }
        }
    },
    async previousIndex() {
        let index = this.indexes.find(i => i.entityId == this.entityId)
        let ix = this.indexes.indexOf(index)

        if (ix > 0) {
            let newIndex = this.indexes[ix - 1]
            this.setEntity(newIndex.entityId)
        } else {
            await this.getIndexPreviousPage()
            if (this.indexes.some(Boolean)) {
                this.setEntity(this.indexes[this.indexes.length - 1].entityId)
            }
        }
    },
    async getIndexNextPage() {
        this.indexesPage++
        await this.getIndexes();
    },
    async getIndexPreviousPage() {
        this.indexesPage--
        await this.getIndexes();
    },
    nextPage() {
        if (this.page < this.nPages - 1) {
            this.page++;
            this.getMainValues()
        }
    },
    previousPage() {
        if (this.page > 0) {
            this.page--;
            this.getMainValues()
        }
    },
    graph(a) {
        if (this.isConfig) {
            this.mode = 5
            this.getAttributeConfig(a.attributeId)
            this.getAttributeUse(a.attributeId)
        } else {
            if (this.entityId > 0 && (a.dataTypeId == 2 || a.dataTypeId == 7)) {
                this.getDatedValues(a)
            }
        }
    },
    graphZoom(dir) {
        if (this.graphData && this.graphAttribute) {
            let zf = this.graphData.zoomFraction
            if (dir > 0 && zf <= 0.5) {
                zf = zf * 2.0
            }
            else if (dir < 0 && this.graphData.datedValues?.length * zf >= 4) {
                zf = zf / 2.0
            }
            this.graphData.zoomFraction = zf
            this.graphData.plot()
        }
    },
    async getDatedValues(a) {
        fetchWrapper
            .get('DatedValues/' + this.entityId + ',' + a.attributeId)
            .then((values) => {
                this.graphData.datedValues = values
                this.graphData.plot()
                this.graphAttribute = a
            })
            .catch((error) => console.error(error));
    },
    async getAttributeConfig(attributeId) {
        this.loadMessage = "loading.."
        fetchWrapper
            .get('AttributeConfig/' + attributeId)
            .then((data) => {
                this.attributeConfig = data
                this.lookupsPage = 0
                this.loadMessage = null
            })
            .catch((error) => console.error(error));
    },

    async getAttributeUse(attributeId) {
        this.attributeUse = new attributeUse()
        fetchWrapper
            .get('AttributeUse/' + attributeId)
            .then((data) => {
                this.attributeUse = data
                this.loadMessage = null
            })
            .catch((error) => console.error(error));
    },
    async getMainValues() {
        fetchWrapper
            .get('ViewValues/' + this.view.id + ',' + this.entityId + ',' + this.page)
            .then((values) => this.values = values)
            .catch((error) => console.error(error));
    },
    selectTargetClass(a) {
        if (this.isConfig) {
            return a.attributeId == this.attributeConfig.id ? 'selected' : 'config'
        } else if (this.entityId > 0) {
            return (a.dataTypeId == 2 || a.dataTypeId == 7) ? 'clickable' : 'hidden'
        }
        else return 'hidden'
    },
    selectTooltip(a) {
        if (this.isConfig) {
            return 'click here to display config data for ' + a.name
        } else if (this.entityId > 0) {
            return a.dataTypeId == 2 || a.dataTypeId == 7 ? 'click here to graph ' + a.name : null
        }
        return null
    },
    async getIdValues() {
        fetchWrapper
            .get('ViewValues/' + this.entityType.idlineViewId + ',' + this.entityId + ',0')
            .then((values) => this.idValues = values)
            .catch((error) => console.error(error));
    },
    getViews() {
        let search = this.viewSearch
        this.viewsPage = 0;
        this.menuHistory = []

        if (search?.length > 0) {
            this.views = this.entityType.views.filter(v => v.captions.some(c => c.text.toLowerCase().includes(search.toLowerCase())));

            return;
        }
        this.views = this.entityType.views;

    },
    indexSearchChanged() {
        //debounce
        let timer = setTimeout(() => {
            this.indexesPage = 0;
            this.getIndexes();
        }, 500);
    },
    setView(view) {
        this.view = view
        this.graphAttribute = null
        this.page = 0
        if (this.view.isDated && this.menu.menuItems?.length === 0) {
            this.menu.menuItems.push({ "name": "Up screen", "function": "2", "seq": 1, "nextMenuId": 0, "startMenuId": 0 })
            this.menu.menuItems.push({ "name": "Down screen", "function": "3", "seq": 2, "nextMenuId": 0, "startMenuId": 0 })
        }
        if (this.entityId > 0) {
            this.getMainValues()
            this.getNPages()
        }
    },
    setEntity(entityId) {
        this.entityId = entityId
        this.page = 0
        if (indexTimer) {
            clearTimeout(indexTimer)
        }
        indexTimer = setTimeout(() => {
            this.indexesPage = 0;
            this.indexes = []
            this.indexSearch = ''

        }, 10000);


        if (this.entityId > 0) {
            this.getIdValues()
            if (this.view.id > 0) {
                if (this.graphAttribute) {
                    this.getDatedValues(this.graphAttribute)
                } else {
                    this.getMainValues()
                    this.getNPages()
                }
                return
            }
        }
    },
    getNPages() {
        fetchWrapper
            .get('NPages/' + this.view.id + ',' + this.entityId)
            .then((data) => this.nPages = data)
            .catch((error) => console.error(error));
    },
    getMenu(id) {
        if (id != this.menu.id) {

            if (id < 0) {
                if (this.menuHistory.some(Boolean)) {
                    this.menu = this.menuHistory.pop()
                } else {
                    this.goHome()
                }
                return
            }

            let lastMenu = this.menuHistory.some(Boolean) ? this.menuHistory[this.menuHistory.length - 1] : null;

            if (lastMenu == null ||
                lastMenu.id !== this.menu.id
            ) {
                this.menuHistory.push(this.menu)
            }

            fetchWrapper
                .get('Menu/' + id)
                .then((data) => {
                    if (data.menuItems?.length === 0) {
                        data.menuItems.push({ "name": "Prev. menu", "function": "", "seq": 3, "nextMenuId": -2, "startMenuId": 0 })
                        if (this.view.isDated) {
                            data.menuItems.push({ "name": "Up screen", "function": "2", "seq": 1, "nextMenuId": 0, "startMenuId": 0 })
                            data.menuItems.push({ "name": "Down screen", "function": "3", "seq": 2, "nextMenuId": 0, "startMenuId": 0 })
                        }
                    }
                    this.menu = data
                })
                .catch((error) => console.error(error));
        }
    },
    goHome() {
        this.menu = this.userStarter.menu;
        this.menuHistory = []
        this.setEntityType(this.entityTypes.find(t => t.id == this.userStarter.entityTypeId))
        this.setIndexType(this.entityType.indexTypes.find(t => t.id == this.userStarter.indexTypeId))
        this.mode = 1
        this.viewSearch = ""
        this.isAdvanced = false
        this.graphAttribute = null
    },
    doMenuItem(item) {
        item.isSelected = true
        let timer = setTimeout(() => {
            item.isSelected = false
        }, 500);
        switch (item.function) {
            case 'SCRN':
                this.setView(this.entityType.views.find(v => v.id == item.parameter1))
                break;
            case 'CHGE':
                this.setEntityType(this.entityTypes.find(e => e.id == item.parameter1))
                break;
            case '1':
                let page = window.prompt('go to page:')
                if (page > 0 && page <= this.nPages) {
                    this.page = page - 1
                }
                break;

            case '2':
                this.previousPage()
                break;

            case '3':
                this.nextPage()
                break;
            default:
            // code block
        }
        if (item.nextMenuId !== 0) {
            this.getMenu(item.nextMenuId)
        } else if (item.startMenuId !== 0) {
            this.getMenu(item.startMenuId)
        }
    },
    setAdvanced() {
        if (!this.isAdvanced) {
            this.isAdvanced = true
            this.menu = JSON.parse('{ "id":0, "name":"ADVANCED","menuItems":[]}')
            this.mode = 2
            this.getViews()
            if (this.view.isDated && this.menu.menuItems.length === 0) {
                this.menu.menuItems.push({ "name": "Up screen", "function": "2", "seq": 1, "nextMenuId": 0, "startMenuId": 0 })
                this.menu.menuItems.push({ "name": "Down screen", "function": "3", "seq": 2, "nextMenuId": 0, "startMenuId": 0 })
            }
        }
    },
    clear() {
        this.view = {}
        this.idValues = []
        this.values = []
    },
    keydown(e) {
        switch (e.key) {
            case "Escape":
                this.logout()
                break
            case "ArrowUp":
                this.previousIndex()
                return;
            case "ArrowDown":
                this.nextIndex()
                return;
            case "PageUp":
                this.previousPage()
                return;
            case "PageDown":
                this.nextPage()
                return;
            case "Enter":
                if (this.mode > 1) this.goHome()
                return;
            case "HOME":
                this.goHome()
                return
            case "0":
                let timer = setTimeout(() => {
                    document.getElementById("#searchEntity").focus();
                }, 500);
                this.mode = 1
                return
            default:
        }

        if (Number.isNaN(e.key)) return
        let keyMap = [6, 7, 8, 3, 4, 5, 0, 1, 2]
        let menuId = keyMap[e.key - 1]
        if (menuId === undefined) return
        let menuItem = this.menu.menuItems.find(i => i.seq == menuId)
        if (menuItem === undefined) {
            return;
        }
        if (menuItem !== null) {

            //document.getElementById("#searchEntity").blur();
            this.doMenuItem(menuItem)
        }
    },
    viewCaption(text) {
        if (this.viewSearch?.length > 0) {
            let reg = new RegExp(`${this.viewSearch}`, 'gi');

            return text.replace(reg, "<tspan>$&</tspan>")
        }
        return text
    }

})

createApp({ app }).mount()

var indexTimer
var loginTimer

function resetLoginTimer() {

    if (loginTimer) {
        clearTimeout(loginTimer)
    }
    loginTimer = setTimeout(() => {
        app.logout()
        location.reload();
    }, 900000);
}

class FetchWrapper {
    constructor(baseUrl) {
        this.baseUrl = baseUrl;
        this.token = null;
    }

    async get(url) {
        if (this.token == null) return;
        resetLoginTimer()
        const response = await fetch(`${this.baseUrl}${url}`,
            {
                method: 'get',
                headers: {
                    'Accept': 'application/json, text/plain, */*',
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + this.token,
                }
            });
        this.status = response.status;
        if (!response.ok) {
            throw new Error("Network response was not ok");
        }
        return response.json();
    }


    async put(url, data) {
        const response = await fetch(`${this.baseUrl}${url}`, {
            method: 'PUT',
            body: JSON.stringify(data),
            headers: {
                'Content-Type': 'application/json',
            },
        });
        if (!response.ok) {
            console.log(response.status);
        }
        return response.json();
    }

    async post(url, data) {
        const response = await fetch(`${this.baseUrl}${url}`, {
            method: 'POST',
            body: JSON.stringify(data),
            headers: {
                'Content-Type': 'application/json',
            },
        });
        if (!response.ok) {
            console.log(response.status);
            return;
        }
        resetLoginTimer()
        return response.json();
    }

    async delete(url) {
        const response = await fetch(`${this.baseUrl}${url}`, {
            method: 'DELETE',
        });
        return response.json();
    }
}

const fetchWrapper = new FetchWrapper(window.location);

function attributeUse() {
    this.nEntities = "..."
    this.perEntity = "..."
    this.latest = "..."
} 