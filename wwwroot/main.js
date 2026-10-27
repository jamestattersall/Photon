import { createApp, reactive } from './petite-vue.js?module' // 'https://unpkg.com/petite-vue?module'
import { graphics } from './graphics.js'

const app = reactive({
    charWidth: 12, // number of arbitary screen units in one monotype character width
    fontSize: 20,  // a.k.a font height = charWidth * 5/3 for monotype font
    lineHeight: 24, // space between lines = charWidth * 2 on a DEC VT-100
    editingValue: null,
    entityType: {},
    view: {},
    views: [],
    attribute: {},
    value: {},
    viewsPage: 0,
    viewSearch: '',
    indexType: {},
    indexTypePage: 0,
    indexes: [],
    indexesPage: 0,
    indexSearch: '',
    entityTypes: [],
    entityId: 0,
    idValues: [],
    page: 0,
    nPages: 0,
    userStarter: { id: 1, functionId: 1, priv: 255, parameter: 50, entityTypeId: 1, idLineViewId: 128, indexTypeId: 1 },
    pwd: 'UFCCL',
    loginMessage: "",
    loadMessage: "",
    menu: { id: 0, name: "", menuItems: [] },
    menuHistory: [],
    sideMode: 0,
    mainMode: 1,
    isAdvanced: false,
    isConfig: false,
    isEditable: false,
    attributeConfig: { id: 0 },
    attributeUse: new AttributeUse(),
    lookupsPage: 0,
    graphData: graphics,
    longText: '',
    valueClicked(a, v) {
        if (this.isEditable) {
            this.edit(a, v)
        }
        else if (a.dataTypeId == 2 || a.dataTypeId == 7) {
            this.graph(a);
        }
        else if (a.dataTypeId == 6) {
            this.showLongText(v)
        }
    },
    showLongText(v) {
        this.getLongText(v)
        this.mainMode = 3;
    },
    async edit(a, v) {
        var vm = this;
        var dl = a.dataTypeId === 4 ? a.displayLength + 5 : a.displayLength;

        var ac = {
            id: a.attributeId,
            x: a.x,
            dataTypeId: a.dataTypeId,
            displayLength: dl
        }

        if (a.dataTypeId == 3) {
            await vm.getLookups(a.attributeId, 0, 12)
            let ll = this.lookups.length
            let lo = vm.lineOrdinal(a, v.seq) - 1
            ac.height = (ll > 10) ? 10 : ll
            ac.y = lo
            if (ac.height + ac.y > 21) {
                ac.y = 21 - ac.height
            }
        } else {
            ac.y = vm.lineOrdinal(a, v.seq) - 1
            ac.height = 1
        }

        this.attribute = ac;
        this.editingValue = v;
    },
    nextLookups() {
        this.lookupsPage++
        this.getLookups(this.attribute.id, this.lookupsPage, 20)

    },
    previousLookups() {
        this.lookupsPage--
        this.getLookups(this.attribute.id, this.lookupsPage, 20)
    },
    lookupClicked(lookup) {
        let nm = lookup.name.substring(0, this.attribute.displayLength)
        this.value.text = nm;
        this.value.lookupId = lookup.id;
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
        this.sideMode = 0;
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
            .then((data) => {
                if (data == null) {
                    this.loginMessage = 'Invalid password'
                } else {
                    this.loginMessage = "Login succeeded. Loading..";
                    this.userStarter = data.userStarter
                    fetchWrapper.token = data.token
                    console.log('token: ' + data.token)
                    this.getEntityTypes();
                    resetLoginTimer()
                }
            })
            .catch((error) => console.error(error));

    },
    //init() {
    //  this.tryLogin();
    //},
    getFormattedDate(date, a) {
        //console.log(JSON.stringify(date));
        date = date ?? '';
        if (date.length < 8) return '';

        let [year, month, day] = date.split('-');
        if (a.displayLength < 10) {
            year = year.substring(2, 4)
        }
        return `${day}.${month}.${year}` ?? '';
    },
    handleCompositeInput(e, v) {
        let raw = e.target.value;

        raw = raw.replace(/[^<>0-9.]/g, '');

        // 2. Ensure < or > can only appear at the very beginning
        raw = raw.replace(/(?!^)[<>]/g, '');

        raw = raw.replace(/^([<>])[<>]+/g, '$1');

        raw = raw.replace(/(?<=\d)[^\d.]+/g, '');

        raw = raw.replace(/(?<=\..*)\.$/, '')
        v = raw;
        e.target.value = raw;
    },
    handleCompositeTime(e) {
        const value = e.target.value;
        // 1. Regex for progressive matching (allows building 'PRE', 'POST', or 'hh:mm')
        // Supports typing 'P' -> 'PR' -> 'PRE', or '1' -> '12' -> '12:' -> '12:3' -> '12:34'
        const progressiveRegex = /^(?:P(?:R(?:E)?)?|P(?:O(?:S(?:T)?)?)?|(?:[01]\d?|2[0-3]?)(?::[0-5]?\d?)?)$/;

        // 2. If empty, or if it matches the allowed progressive pattern, accept it
        if (value === '' || progressiveRegex.test(value)) {
            this.editingValue.text = value;
        } else {
            // 3. Reject the change: force the input element back to the last valid state
            e.target.value = this.editingValue.text;
        }
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
    async getLookups(attributeId, page, rows) {
        return await fetchWrapper
            .get('Lookups/' + attributeId + ',' + page + ',' + rows)
            .then(data => {
                console.log('lookups:' + JSON.stringify(data));
                this.lookups = data
            })
            .catch((error) => {
                console.log('error1' + error)
                this.lookups = [];
            });
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
            this.lookupSearch = '';
            this.clear()
            this.sideMode = 1
            this.getIndexes()
            this.getViews()
        }
    },
    setIndexType(indexType) {
        this.indexType = indexType
    },
    async getIndexes() {
        if (this.indexSearch?.length >= 1 || typeof this.indexSearch === 'number') {
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
            this.view.activeAttributes.forEach(a => { a.values = []; })
            this.page++;
            this.getMainValues()
        }
    },
    previousPage() {
        if (this.page > 0) {
            this.view.activeAttributes.forEach(a => { a.values = []; })
            this.page--;
            this.getMainValues()
        }
    },
    showConfig(a) {
        this.sideMode = 5
        this.getAttributeConfig(a.attributeId)
        this.getAttributeUse(a.attributeId)
    },
    graph(a) {
        if (this.entityId > 0 && (a.dataTypeId == 2 || a.dataTypeId == 7)) {
            this.graphData.zoomFraction=1
            fetchWrapper
                .get('DatedValues/' + this.entityId + ',' + a.attributeId)
                .then((data) => {
                    this.graphData.datedValues = data
                    this.graphData.plot()
                    this.attribute = a
                    this.mainMode = 2;
                })
                .catch((error) => console.error(error));
        }
    },
    graphZoom(dir) {
        if (this.graphData && this.mainMode == 2) {
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
        this.attributeUse = new AttributeUse()
        fetchWrapper
            .get('AttributeUse/' + attributeId)
            .then((data) => {
                this.attributeUse = data
                this.loadMessage = null
            })
            .catch((error) => console.error(error));
    },
    async getMainValues() {
        try {

            this.editingValue = null;
            const url = `ViewValues/${this.view.id},${this.entityId},${this.page}`;

            const vals = await fetchWrapper.get(url) || [];
            //console.log(JSON.stringify(vals, null, 2));
            const valuesMap = Map.groupBy(vals, v => v.attributeId);

            for (let a of this.view.activeAttributes) {
                a.values = valuesMap.get(a.attributeId) || [];
            };
            //console.log(JSON.stringify(this.view.activeAttributes, null, 2));

            this.isEditable = false;

        } catch (error) {
            console.error("Error fetching or mapping data:", error);
        }
    },
    async getLongText(v) {
        fetchWrapper
            .get(`LongText/${this.entityId},${v.attributeId},${v.seq}`)
            .then(data => {
                this.longText = data.text;
            })
    },
    lineOrdinal(a, seq) {
        return (a.y + seq - this.view.nRows * this.page - 1)
    },
    async getIdValues() {
        fetchWrapper
            .get('IdValues/' + this.entityType.idlineViewId + ',' + this.entityId)
            .then((data) => this.idValues = data)
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
        this.mainMode = 1;
        this.view.activeAttributes = structuredClone(JSON.parse(JSON.stringify(this.view.viewAttributes)))
        this.page = 0
        if (this.view.dateAttributeId > 0 && this.menu.menuItems?.length === 0) {
            this.menu.menuItems.push({ "name": "Up screen", "function": "2", "seq": 1, "nextMenuId": 0, "startMenuId": 0 })
            this.menu.menuItems.push({ "name": "Down screen", "function": "3", "seq": 2, "nextMenuId": 0, "startMenuId": 0 })
        }
        if (this.entityId > 0) {
            this.getMainValues();
            this.getNPages();
        }
    },
    setEditable() {
        if (this.entityId === 0) { return; }

        const dateAttr = this.view.activeAttributes.find(da => da.attributeId === this.view.dateAttributeId);
        const rowCount = dateAttr?.values?.length || 1;
        (this.view.activeAttributes || []).forEach(att => {
            //  const valuesToGroup = att.values || [];
            //   const mapVals = Map.groupBy(valuesToGroup, x => x.seq);
            const startRow = (this.page) * this.view.nRows + 1;
            let nRows = this.view.nRows;
            if (rowCount < nRows) { nRows = rowCount; };
            const endRow = startRow + nRows;
            for (let i = startRow; i < endRow; i++) {
                if (att.values.find(v => v.seq === i) === undefined) {
                    //att.values = att.values || [];
                    att.values.push({ attributeId: att.attributeId, seq: i, text: null });
                }
            }
        });
        this.view.originalAttributes = structuredClone(JSON.parse(JSON.stringify(this.view.activeAttributes)));

        this.isEditable = true;
    },

    saveEdits() {
        if (!this.isEditable) return;

        let editedValues = [];

        this.view.activeAttributes.forEach((aAtt, ix) => {
            let oAtt = this.view.originalAttributes[ix];

            aAtt.values.forEach(aVal => {
                let oVal = oAtt.values.find(v => v.seq === aVal.seq);

                if (oVal.text !== aVal.text) {
                    editedValues.push({
                        attributeId: aAtt.attributeId,
                        seq: aVal.seq,
                        originalText: oVal.text,
                        newText: aVal.text
                    });
                }
            });
        });
        this.isEditable = false;
        this.editingValue = null;
        console.log(JSON.stringify(editedValues));
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
                if (this.mainMode == 2 && this.attribute) {
                    this.graph(this.attribute)
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
                        if (this.view.dateAttributeId > 0) {
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
        this.sideMode = 1
        this.viewSearch = ""
        this.isAdvanced = false
        this.isConfig = false
        this.isEditable = false
        this.mainMode = 1;
        this.clear
    },
    doMenuItem(item) {
        item.isSelected = true
        let timer = setTimeout(() => {
            item.isSelected = false
        }, 500);
        switch (item.function) {
            case 'SCRN':
                this.setView(this.entityType.views.find(v => v.id == item.parameter1))
                if (item.flags & 32 === 32) this.setEditable();
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

            case '5':
                this.setEditable();
                break;

            case '6':
                this.saveEdits();
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
            this.menu = JSON.parse('{ "id":0, "name":"ADVANCED", "menuItems":[]}')
            this.sideMode = 2
            this.getViews()
            if (this.view.dateAttributeId > 0 && this.menu.menuItems.length === 0) {
                this.menu.menuItems.push({ "name": "Up screen", "function": "2", "seq": 1, "nextMenuId": 0, "startMenuId": 0 })
                this.menu.menuItems.push({ "name": "Down screen", "function": "3", "seq": 2, "nextMenuId": 0, "startMenuId": 0 })
            }
        }
    },
    clear() {
        this.view = {}
        this.idValues = []
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
                if (this.sideMode > 1) this.goHome()
                return;
            case "HOME":
                this.goHome()
                return
            case "0":
                if (this.sideMode != 1 && !this.isEditable) {
                    let timer = setTimeout(() => {
                        document.getElementById("#searchEntity").focus();
                    }, 500);
                    this.sideMode = 1
                }
                return
            default:
        }
        if (!this.isAdvanced && !this.isEditable) {
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
            console.log(url);
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
// This regex matches any progressive step of 'PRE', 'POST', or 'HH:MM'
const allowedProgressiveInput = /^(?:P(?:R(?:E)?)?|P(?:O(?:S(?:T)?)?)?|(?:[01]\d?|2[0-3]?)(?::[0-5]?\d?)?)$/

function AttributeUse() {
    this.nEntities = "..."
    this.perEntity = "..."
    this.latest = "..."
}
function CustomInput(initialLabel, initialPlaceholder = '') {
    return {
        label: initialLabel,
        placeholder: initialPlaceholder,
        text: '',
    }
}