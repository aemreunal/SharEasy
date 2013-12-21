/*
 * After creating a Mobile Service for the app, the following script should
 * be written to the SharedItem table's read script, instead of the standard
 * one. The code is followed by a non-functional script, kept for referral 
 * purposes, as I believe it's a more efficient one.
 */

var friends = [];
var friendsCount = 0;
var items = [];
var itemsCount = 0;

var fbUserID;

var requestObj;
var httpRequest = require('request');

function read(query, user, request) {
    console.log("Beginning to read.");
    requestObj = request;

    fbUserID = user.userId.substring(9, user.userId.length);

    var url = 'https://graph.facebook.com/' + fbUserID + '/friends?fields=id&limit=5000';
    url += "&access_token=" + user.getIdentities().facebook.accessToken;

    console.log("Attempting Facebook request.");
    httpRequest.get({
        url: url
    }, function (err, response, body) {
        if (err) {
            console.log("Facebook connection error.");
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, 'Unable to connect to facebook.');
        } else if (response.statusCode !== 200) {
            console.log("Bad request.");
            console.log(response);
            request.respond(statusCodes.BAD_REQUEST, 'Bad request.');
        } else {
            console.log("Successfully got the response.");
            getFriendIDs(JSON.parse(body));
        }
    });
}

function getFriendIDs(friendsJSON) {
    console.log("Attempting getting friend IDs.");
    friends[friendsCount] = fbUserID;
    friendsCount++;
    var dataArray = friendsJSON.data;
    for (var i = 0; i < dataArray.length; i++) {
        friends[friendsCount] = dataArray[i].id;
        friendsCount++;
    }
    console.log("Got friend IDs.");
    createResponse();
}

function createResponse() {
    console.log("Attempting to create response.");
    requestObj.execute({
        success: function (results) {
            console.log("Got table.");
            var items = [];

            results.forEach(function (r) {
                if (friends.indexOf(r.facebookUserID) > -1) {
                    items[items.length] = r;
                }
            });
            results = items;
            console.log("Got all items.");
            requestObj.respond();
        }
    });
}

/* ---------------------------------------------------------------
 * The non-functional code:

var friends = [];
var friendsCount = 0;
var items = [];
var itemsCount = 0;

var fbUserID;

var requestObj;    
var httpRequest = require('request');

function read(query, user, request) {
    console.log("Beginning to read.");
    requestObj = request;
    
    fbUserID = user.userId.substring(9,user.userId.length);
    
    var url = 'https://graph.facebook.com/' + fbUserID + '/friends?fields=id&limit=5000';
    url += "&access_token=" + user.getIdentities().facebook.accessToken;
    
    console.log("Attempting Facebook request.");
    httpRequest.get({
        url: url
    }, function(err, response, body) {
        if (err) {
            console.log("Facebook connection error.");
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, 'Unable to connect to facebook.');
        } else if (response.statusCode !== 200) {
            console.log("Bad request.");
            console.log(response);
            request.respond(statusCodes.BAD_REQUEST, 'Bad request.');
        } else {
           console.log("Successfully got the response.");
           getFriendIDs(JSON.parse(body));
        }
    });
}

function getFriendIDs(friendsJSON) {
    console.log("Attempting getting friend IDs.");
    friends[friendsCount] = fbUserID;
    friendsCount++;
    var dataArray = friendsJSON.data;
    for (var i = 0; i < dataArray.length; i++) {
        friends[friendsCount] = dataArray[i].id;
        friendsCount++;
    }
    console.log("Got friend IDs.");
    createResponse();
}

var currentFriend;

function createResponse() {
    console.log("Attempting to create response.");
    var itemsTable = tables.getTable('SharedItem');
    console.log("Got table.");
    
    for (currentFriend = 0; currentFriend < friends.length; currentFriend++) {
        itemsTable
            .where({ facebookUserID: friends[currentFriend] })
            .read({ success: getItems });
    }
    console.log("Got all items.");
    requestObj.respond(statusCodes.OK, items);
}

function getItems(results) {
    console.log("Attempting to get items of user " + currentFriend + ".");
    for (var j = 0; j < results.length; j++) {
        items[itemsCount] = results[j];
        itemsCount++;
    }
}

 */