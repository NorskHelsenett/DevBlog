Creating Downloadable bundles in the frontend

Sometimes it's nice to be able to create a form to help a user generate a collection of files with some customization. For instance if you want to help them create helm charts in the organizations preferred style, while helping them avoid issues with the wrong capitalization used in the wrong places.

Our challenge is that while browsers have a nice built in API to let users download a file containing some bytes they have polished the input for in our webapp, there is no equivalent to download a collection of files, or more ideally a collection structured in folders. An easy solution would be to set up a backend, and have the users ship their inputs off their devices to the one we control. Then we could centrally compile their inputs, with some special sauce to get the desired files and folders. The final step then would be to bundle them up, with a zip or something, and send it back to the browser as a single file to download.

However, while it's easy to fire up, it's far from simple. And we introduce a lot of back and forth traffic for the user to wait for, which is not needed. Also suddenly we have to consider the implications of users submitting things to a shared place we now have to harden against the insanity only those who interface with users get to experience.

Our question then becomes, could we do it locally, on the users device?

After a quick search it turns out that yes, other people have thought of this! For instance there is [jszip](https://github.com/Stuk/jszip). However, it's about [12 thousand lines](https://github.com/Stuk/jszip/blob/main/dist/jszip.js), and [95kB in size when minified](https://github.com/Stuk/jszip/blob/main/dist/jszip.min.js), which seems excessive to audit, maintain, and burden users ram with. Unfortunately most libraries generating [zip](https://en.wikipedia.org/wiki/ZIP_(file_format)) files seems to suffer from the same issues, so it appears to be inherent to the zip standards complexity.

Which raises the question, weren't there simpler times, before we hyper optimized compression algorithms and video codecs, but people had come up with clever ways to package a bundle of files into in a single archive file for storage and shipment, that will surely work well enough for the small utilities we're making?

And sure enough, after digging in our memory, we recall that there was this thing called [tar](https://en.wikipedia.org/wiki/Tar_(computing)) in the before times. While storing *Tape ARchive* formatted files on magnetic tape drives is not particularly relevant, the core functionality of turning a collection of files into a single continuous blob is perfect for our use case.

And since `tar` support was [added to Windows in 2018](https://devblogs.microsoft.com/commandline/windows10v1803/#tar-and-curl-with-windows-10), it's natively cross platform. The only thing to watch out for is that while Linux and MacOs GUI users can double click the files to open them, you might have to add a small note for the users still on the legacy Windows platform that they have to run `tar -xvf <filename.tar>` in their terminal to unpack the output, unless they install some third party utility like [7zip](https://en.wikipedia.org/wiki/7-Zip).

A dive into tar implementations in js yielded [Ankit Rohatgi](https://github.com/ankitrohatgi)s MIT licensed [tarballjs](https://github.com/ankitrohatgi/tarballjs). At a modest [404 lines or 12kB unminified](https://github.com/ankitrohatgi/tarballjs/blob/master/tarball.js) it is much more palatable than the alternative zip implementations!

We can also make it even smaller, because it comes with code for unpacking tar archives, enabling you to ingest them in your web app and work on the files in js, which we don't really care about. Throwing out that, we sit on a sweet 260 lines of unminifed js. Awesome!

The only issue is naming, because it can be nice to let users name the files for instance, but the original tar spec limits file and directory names to 100 characters. Thankfully [GNU](https://en.wikipedia.org/wiki/GNU_Project) implemented a neat workaround for this, which seems to be supported more or less everywhere, and fits into an addittional function call putting long names into special files in the archives to which is assigned the filetype "LongLink".

In practice we end up with the code shown below:

```js
function exampleOfUse() {
  let tar = new TarWriter();
  tar.addFolder("example_dir/");
  tar.addTextFile("example_dir/first.txt", "first sample text");
  tar.addTextFile("example_dir/second.txt", "second sample text");
  var longName = "";
  for (let i = 0; i < 15; i++) {
    longName += `${i}abcdefghij`;
  }
  tar.addTextFile(`${longName}/${longName}.txt`, "obscenely long file and folder names!")
  tar.download("example.tar");
}

class TarWriter {
    // Based on https://github.com/ankitrohatgi/tarballjs @ commit 1134d90ca7f1a9fe00e124a0f0cb4d055a1b9cfa on 2025-07-02 MIT Licensed by Ankit Rohatgi
    constructor() {
        this.fileData = [];
    }

    addTextFile(name, text, opts) {
        this._handleLongFileNameGnuWay(name, opts);
        let te = new TextEncoder();
        let arr = te.encode(text);
        this.fileData.push({
            name: name,
            array: arr,
            type: "file",
            size: arr.length,
            dataType: "array",
            opts: opts
        });
    }

    addFileArrayBuffer(name, arrayBuffer, opts) {
        this._handleLongFileNameGnuWay(name, opts);
        let arr = new Uint8Array(arrayBuffer);
        this.fileData.push({
            name: name,
            array: arr,
            type: "file",
            size: arr.length,
            dataType: "array",
            opts: opts
        });
    }

    addFile(name, file, opts) {
        this._handleLongFileNameGnuWay(name, opts);
        this.fileData.push({
            name: name,
            file: file,
            size: file.size,
            type: "file",
            dataType: "file",
            opts: opts
        });
    }

    addFolder(name, opts) {
        this._handleLongFileNameGnuWay(name, opts);
        this.fileData.push({
            name: name,
            type: "directory",
            size: 0,
            dataType: "none",
            opts: opts
        });
    }

    async download(filename) {
        let blob = await this.writeBlob();
        let $downloadElem = document.createElement('a');
        $downloadElem.href = URL.createObjectURL(blob);
        $downloadElem.download = filename;
        $downloadElem.style.display = "none";
        document.body.appendChild($downloadElem);
        $downloadElem.click();
        document.body.removeChild($downloadElem);
    }

    async writeBlob(onUpdate) {
        return new Blob([await this.write(onUpdate)], {"type":"application/x-tar"});
    }

    write(onUpdate) {
        return new Promise((resolve,reject) => {
            this._createBuffer();
            let offset = 0;
            let filesAdded = 0;
            let onFileDataAdded = () => {
                filesAdded++;
                if (onUpdate) {
                    onUpdate(filesAdded / this.fileData.length * 100);
                }
                if(filesAdded === this.fileData.length) {
                    let arr = new Uint8Array(this.buffer);
                    resolve(arr);
                }
            };
            for(let fileIdx = 0; fileIdx < this.fileData.length; fileIdx++) {
                let fdata = this.fileData[fileIdx];
                // write header
                this._writeFileName(fdata.name, offset);
                this._writeFileType(fdata.type, offset);
                this._writeFileSize(fdata.size, offset);
                this._fillHeader(offset, fdata.opts, fdata.type);
                this._writeChecksum(offset);

                // write file data
                let destArray = new Uint8Array(this.buffer, offset+512, fdata.size);
                if(fdata.dataType === "array") {
                    for(let byteIdx = 0; byteIdx < fdata.size; byteIdx++) {
                        destArray[byteIdx] = fdata.array[byteIdx];
                    }
                    onFileDataAdded();
                } else if(fdata.dataType === "file") {
                    let reader = new FileReader();

                    reader.onload = (function(outArray) {
                        let dArray = outArray;
                        return function(event) {
                            let sbuf = event.target.result;
                            let sarr = new Uint8Array(sbuf);
                            for(let bIdx = 0; bIdx < sarr.length; bIdx++) {
                                dArray[bIdx] = sarr[bIdx];
                            }
                            onFileDataAdded();
                        };
                    })(destArray);
                    reader.readAsArrayBuffer(fdata.file);
                } else if(fdata.type === "directory") {
                    onFileDataAdded();
                }

                offset += (512 + 512*Math.trunc(fdata.size/512));
                if(fdata.size % 512) {
                    offset += 512;
                }
            }
        });
    }

    _createBuffer() {
        let tarDataSize = 0;
        for(let i = 0; i < this.fileData.length; i++) {
            let size = this.fileData[i].size;
            tarDataSize += 512 + 512*Math.trunc(size/512);
            if(size % 512) {
                tarDataSize += 512;
            }
        }
        let bufSize = 10240*Math.trunc(tarDataSize/10240);
        if(tarDataSize % 10240) {
            bufSize += 10240;
        }
        this.buffer = new ArrayBuffer(bufSize);
    }

    _handleLongFileNameGnuWay(name, opts) {
      // https://github.com/ankitrohatgi/tarballjs/issues/1#issuecomment-1229889378
      if (name.length > 100) {
        let te = new TextEncoder();
        let arr = te.encode(name);
        this.fileData.push({
          name: '././@LongLink',
          array: arr,
          type: 'longLink',
          size: arr.length,
          dataType: 'array',
          opts: opts,
        });
      }
    }

    _writeString(str, offset, size) {
        let strView = new Uint8Array(this.buffer, offset, size);
        let te = new TextEncoder();
        if (te.encodeInto) {
            // let the browser write directly into the buffer
            let written = te.encodeInto(str, strView).written;
            for (let i = written; i < size; i++) {
                strView[i] = 0;
            }
        } else {
            // browser can't write directly into the buffer, do it manually
            let arr = te.encode(str);
            for (let i = 0; i < size; i++) {
                strView[i] = i < arr.length ? arr[i] : 0;
            }
        }
    }

    _writeFileName(name, header_offset) {
        // offset: 0
        this._writeString(name, header_offset, 100);
    }

    _writeFileType(typeStr, header_offset) {
        // offset: 156
        let typeChar = "0";
        if(typeStr === "file") {
            typeChar = "0";
        } else if(typeStr === "directory") {
            typeChar = "5";
        } else if (typeStr === 'longLink') {
          // The gnu way of handling long names
          typeChar = 'L';
        }
        let typeView = new Uint8Array(this.buffer, header_offset + 156, 1);
        typeView[0] = typeChar.charCodeAt(0);
    }

    _writeFileSize(size, header_offset) {
        // offset: 124
        let sz = size.toString(8);
        sz = this._leftPad(sz, 11);
        this._writeString(sz, header_offset+124, 12);
    }

    _leftPad(number, targetLength) {
        let output = number + '';
        while (output.length < targetLength) {
            output = '0' + output;
        }
        return output;
    }

    _writeFileMode(mode, header_offset) {
        // offset: 100
        this._writeString(this._leftPad(mode,7), header_offset+100, 8);
    }

    _writeFileUid(uid, header_offset) {
        // offset: 108
        this._writeString(this._leftPad(uid,7), header_offset+108, 8);
    }

    _writeFileGid(gid, header_offset) {
        // offset: 116
        this._writeString(this._leftPad(gid,7), header_offset+116, 8);
    }

    _writeFileMtime(mtime, header_offset) {
        // offset: 136
        this._writeString(this._leftPad(mtime,11), header_offset+136, 12);
    }

    _writeFileUser(user, header_offset) {
        // offset: 265
        this._writeString(user, header_offset+265, 32);
    }

    _writeFileGroup(group, header_offset) {
        // offset: 297
        this._writeString(group, header_offset+297, 32);
    }

    _writeChecksum(header_offset) {
        // offset: 148
        this._writeString("        ", header_offset+148, 8); // first fill with spaces

        // add up header bytes
        let header = new Uint8Array(this.buffer, header_offset, 512);
        let chksum = 0;
        for(let i = 0; i < 512; i++) {
            chksum += header[i];
        }
        this._writeString(chksum.toString(8), header_offset+148, 8);
    }

    _getOpt(opts, opname, defaultVal) {
        if(opts != null) {
            if(opts[opname] != null) {
                return opts[opname];
            }
        }
        return defaultVal;
    }

    _fillHeader(header_offset, opts, fileType) {
        let uid = this._getOpt(opts, "uid", 1000);
        let gid = this._getOpt(opts, "gid", 1000);
        // Chmod permission mode refresher:
        // <owning users permissions><owning groups permissions><everyone elses permissions>
        // r = read, w = write, x = execute
        // Permission | Value | Why value
        //    - - -   |   0   | 0+0+0
        //    - - x   |   1   | 0+0+1
        //    - w -   |   2   | 0+2+0
        //    - w x   |   3   | 0+2+1
        //    r - -   |   4   | 4+0+0
        //    r - x   |   5   | 4+0+1
        //    r w -   |   6   | 4+2+0
        //    r w x   |   7   | 4+2+1
        let mode = this._getOpt(opts, "mode", fileType === "file" ? "664" : "775");
        let mtime = this._getOpt(opts, "mtime", Date.now());
        let user = this._getOpt(opts, "user", "helmgenerator");
        let group = this._getOpt(opts, "group", "helmgenerator");

        this._writeFileMode(mode, header_offset);
        this._writeFileUid(uid.toString(8), header_offset);
        this._writeFileGid(gid.toString(8), header_offset);
        this._writeFileMtime(Math.trunc(mtime/1000).toString(8), header_offset);

        this._writeString("ustar", header_offset+257,6); // magic string
        this._writeString("00", header_offset+263,2); // magic version

        this._writeFileUser(user, header_offset);
        this._writeFileGroup(group, header_offset);
    }
};
```

Which we can use like this

```js

```

Share and enjoy!
