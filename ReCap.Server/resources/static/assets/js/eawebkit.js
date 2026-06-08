// Those are required to the index file from the bootstrap/launcher folder doesn't
// need to make a manual bridge between itself and the wrapper file
var Client = (typeof Client === 'undefined') ? (window.parent || {}).Client : Client;
var DarksporeVersion = (window.parent || {}).DarksporeVersion;

var EAWebKit = {
	isUserAgent: function() {
		return navigator.userAgent.indexOf("EAWebKit") != -1;
	},
	closeWindow: function() {
		if (EAWebKit.isUserAgent()) {
			Client.closeWindow();
		} else {
			window.close();
		}
	},
	openExternalBrowser: function(url) {
		if (EAWebKit.isUserAgent()) {
			Client.openExternalBrowser(url);
		} else {
			window.open(url);
		}
	}
};

var Utils = {
	type: function(obj) {
		return Object.prototype.toString.call(obj);
	},
	isString: function(obj) {
		return Utils.type(obj) === '[object String]';
	},
	isArray: function(obj) {
		return Utils.type(obj) === '[object Array]';
	},
	splitVersionString: function(version) {
		if (version === undefined) return [];
		return version.split(".").map(function(num){ return parseInt(num); });
	},
	compareVersions: function(versionParts, versionArray) {
		if (Utils.isString(versionParts)) versionParts = Utils.splitVersionString(versionParts);
		if (Utils.isString(versionArray)) versionArray = Utils.splitVersionString(versionArray);

		var min = Math.min(versionParts.length, versionArray.length);
		for (var i = 0; i < min; i++) {
			if (versionParts[i] > versionArray[i]) return -1;
			if (versionParts[i] < versionArray[i]) return  1;
		}

		if (versionParts.length > versionArray.length) return -1;
		if (versionParts.length < versionArray.length) return  1;
		return 0;
	}
};



// Original EAWebKit (Darkspore 5.3.0.15) never called .onload and never
// reached readyState 4, so this used to fire the callback on ANY
// onreadystatechange with status 200. The ReCap webview engine (Chromium)
// fires onreadystatechange at readyState 2/3 too — with an empty body —
// so that legacy form called back with "" and JSON.parse blew up.
// Fire once, on either DONE signal, whichever the engine delivers.

var HTTP = {
	attachCallback: function(xmlHttp, callback) {
		var fired = false;
		var fire = function () {
			if (fired || xmlHttp.status !== 200 || callback === undefined) return;
			if (xmlHttp.readyState !== 4) return;
			fired = true;
			callback(xmlHttp.responseText);
		};
		xmlHttp.onreadystatechange = fire;
		xmlHttp.onload = fire;
	},
	get: function(url, obj, callback) {
		var params = obj;
		if (params !== undefined && typeof params === 'object') {
			var str = [];
			for (var p in params)
				if (params.hasOwnProperty(p)) {
					str.push(encodeURIComponent(p) + "=" + encodeURIComponent(params[p]));
				}
			params = str.join("&");
		}

		var xmlHttp = new XMLHttpRequest();
		HTTP.attachCallback(xmlHttp, callback);
		xmlHttp.open("GET", url + (params === undefined ? "" : ("?" + params)), true);
		xmlHttp.send(null);
	},
	post: function(url, obj, callback) {
		var xmlHttp = new XMLHttpRequest();
		HTTP.attachCallback(xmlHttp, callback);
		xmlHttp.open("POST", url, true);
		xmlHttp.setRequestHeader("Content-Type", "application/json");
		xmlHttp.send(JSON.stringify(obj));
	}
};
