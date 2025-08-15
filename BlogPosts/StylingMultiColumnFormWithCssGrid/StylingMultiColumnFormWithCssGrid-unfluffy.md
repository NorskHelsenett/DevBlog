How to make multi section multi column form appearance not induce disgust and confusion.

Have usecase, like generating helm charts adhering to standards (the best ones, ours).
Have need for 3 columns: labels, fields, descriptions/help.
Have multiple sections like this, not everything suitable for same column size.
Want dynamic column size.

Need to wrap grouped items with shared column sizes in same parent element with `display: grid` style.
For input column remaining close to labels column, add style `justify-content: start`.

So now we have css class

```css
.grid-parent {
    display: grid;
    justify-content: start;
}
```

we can use like this

```html
<fieldset class="grid-parent">
    <legend>Group name</legend>
    <div>
        <label for="first_row_input">First Label</label>
        <input type="text" id="first_row_input">
        <span>Description</span>
    </div>
    <div>
        <label for="second_row_input">Second Label</label>
        <input type="text" id="second_row_input">
        <span>Description</span>
    </div>
</fieldset>
```

Now that we have wrapper, we can make rows share grid column sizes by making them subgrids.
To do this need to have style `display: grid` and `grid-template-columns: subgrid`.
To make it 3 column, we need to set the `grid-column` style.
Either tell it to start at 1 and end after 3 before column 4 with using `grid-column: 1/4`, or simply say it spans 3 like this `grid-column: span 3`.
If the description column ends up big spanning multiple rows, prevent input fields being weirdly vertically stretched by setting `align-items: start`.

End up with CSS below.

```css
.subgrid-whole-row-3-col {
    display: grid;
    grid-template-columns: subgrid;
    grid-column: span 3;
    align-items: start;
}
```

Use like this

```html
<fieldset class="grid-parent">
    <legend>Group name</legend>
    <div class="subgrid-whole-row-3-col">
        <label for="first_row_input">First Label</label>
        <input type="text" id="first_row_input">
        <span>Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="second_row_input">Second Label</label>
        <input type="text" id="second_row_input">
        <span>Description</span>
    </div>
</fieldset>
```

Now we need to put each column item in designated column.
The style to use is `grid-column`, the value either just column number, or `start at/end before` like this `grid-column: 1/2`.

End up with these classes

```css
.grid-item-col-1 {
    grid-column: 1;
}
.grid-item-col-2 {
    grid-column: 2;
}
.grid-item-col-3 {
    grid-column: 3;
}
```

which we can apply like so

```html
<fieldset class="grid-parent">
    <legend>Group name</legend>
    <div class="subgrid-whole-row-3-col">
        <label for="first_row_input" class="grid-item-col-1">First Label</label>
        <input type="text" id="first_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="second_row_input" class="grid-item-col-1">Second Label</label>
        <input type="text" id="second_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
</fieldset>
```

Some elements like checkboxes misbehave, and awkwardly place them selves in the middle of their column.
To fix, add `justify-self` and set it to `start`.

The css would be
```css
.justify-self-start {
    justify-self: start;
}
```

which you then use like this

```html
<fieldset class="grid-parent">
    <legend>Group name</legend>
    <div class="subgrid-whole-row-3-col">
        <label for="first_row_input" class="grid-item-col-1">First Label</label>
        <input type="text" id="first_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="second_row_input" class="grid-item-col-1">Second Label</label>
        <input type="text" id="second_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="third_row_checkbox" class="grid-item-col-1">Third label</label>
        <input type="checkbox" id="third_row_checkbox" class="grid-item-col-2 justify-self-start">
        <span class="grid-item-col-3">Description</span>
    </div>
</fieldset>
```

To make it tolerable to observe/use, add some breathing room.
Need to add styling to parent grids.
To space the columns, `column-gap`.
If you want space the rows, use `row-gap`.

Example

```css
.row-gap {
    row-gap: 0.75rem;
}
.col-gap {
    column-gap: 1rem;
}
```

Usage

```html
<fieldset class="grid-parent col-gap row-gap">
    <legend>Group name</legend>
    <div class="subgrid-whole-row-3-col">
        <label for="first_row_input" class="grid-item-col-1">First Label</label>
        <input type="text" id="first_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="second_row_input" class="grid-item-col-1">Second Label</label>
        <input type="text" id="second_row_input" class="grid-item-col-2">
        <span class="grid-item-col-3">Description</span>
    </div>
    <div class="subgrid-whole-row-3-col">
        <label for="third_row_checkbox" class="grid-item-col-1">Third label</label>
        <input type="checkbox" id="third_row_checkbox" class="grid-item-col-2 justify-self-start">
        <span class="grid-item-col-3">Description</span>
    </div>
</fieldset>
```

Bonus text prettiness.
To make text nice, use css `text-align: justify;` and ample amounts of `&shy;` in the html to make spacing break at prettier places (e.g. `in&shy;stead`) when default rendering fails you.
Pair with `max-width` css, for instance `max-width: 50rem;` to make nice columns.
For code/long continous content where you give up and say just do it anywhere no way to make pretty, wrap it and use css `word-break: break-all;`.

Finally, working example shown below, or you can open [WorkingExample](./WorkingExample.html)

```html
<!DOCTYPE html>
<html lang="en">
<body>
    <form name="helm_chart_generating_form" autocomplete="off" autocapitalize="off">
        <!-- ToDo: consider having select for different chart types here -->
        <fieldset class="grid-parent col-gap-m">
            <legend>Chart metadata</legend>
            <div class="subgrid-whole-row-3-col">
                <label for="chart_metadata-chart_name" class="grid-item-col-1">Chart name</label>
                <input type="text" id="chart_metadata-chart_name" required pattern="([a-z0-9]+-)*[a-z0-9]+" value="demo-chart" class="grid-item-col-2">
                <span class="grid-item-col-3">Must be lowercase or numbers, <span class="monospace bg-grey ts-small">-</span> may be used as a word separator. <a href="https://helm.sh/docs/chart_best_practices/conventions/#chart-names">Chart naming docs</a></span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="chart_metadata-chart_description" class="grid-item-col-1">Chart description</label>
                <input type="text" id="chart_metadata-chart_description" class="grid-item-col-2">
                <span class="grid-item-col-3">Optional</span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="chart_metadata-chart_version" class="grid-item-col-1">Chart version</label>
                <input type="text" id="chart_metadata-chart_version" required value="0.0.1" pattern="\d+\.\d+\.\d+" class="grid-item-col-2">
                <span class="grid-item-col-3">Semver</span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="chart_metadata-app_version" class="grid-item-col-1">App version</label>
                <input type="text" id="chart_metadata-app_version" required value="1.0.0" pattern="(\d+)\.(\d+)\.(\d+)" class="grid-item-col-2">
                <span class="grid-item-col-3">Semver</span>
            </div>
        </fieldset>
        <fieldset class="grid-parent col-gap-m row-gap-sm">
            <legend>Image</legend>
            <div class="subgrid-whole-row-3-col">
                <label for="image_repository" class="grid-item-col-1">Image repository</label>
                <textarea id="image_repository" required name="image_repository" rows="1" cols="45" placeholder="container-registry.example.com/..." class="grid-item-col-2">container-registry.example.com/project-x/repository-a</textarea>
                <span class="grid-item-col-3 mw-50 ta-justified">We also use the registry as a proxy cache for non internal images, see <a href="https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-registry/">docs on how to set up proxying</a>. Notable examples are <span class="monospace bg-grey ts-small break-anywhere">container-registry.example.com/dockerhub/</span> and <span class="monospace bg-grey ts-small break-anywhere">container-registry.example.com/mcr/</span></span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="image_tag" class="grid-item-col-1">Image tag</label>
                <input type="text" id="image_tag" required value="latest" class="grid-item-col-2">
                <span class="grid-item-col-3 mw-50 ta-justified">The <a href="https://docs.docker.com/reference/cli/docker/image/tag/">OCI image tag</a>. Note that you can use <a href="https://docs.docker.com/dhi/core-concepts/digests/">image digests</a> in addition to, or in&shy;stead of, tags. If you do you can stuff it (the digest) in the field here. A practical example is that when using the image <span class="monospace bg-grey ts-small break-anywhere">container-registry.example.com/mcr/dotnet/aspnet</span>, in&shy;stead of using the regular tag <span class="monospace bg-grey ts-small break-anywhere">9.0-noble-chiseled</span>, you cou&shy;ld in&shy;stead use <span class="monospace bg-grey ts-small break-anywhere">@sha256:65ac626d0095036e7c477e610ed32e59822992c0b207c95444ef22d455343f4a</span>, or the com&shy;bi&shy;nation <span class="monospace bg-grey ts-small break-anywhere">9.0-noble-chiseled@sha256:65ac626d0095036e7c477e610ed32e59822992c0b207c95444ef22d455343f4a</span></span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="image_http_port" class="grid-item-col-1">Image web port</label>
                <input type="text" id="image_http_port" required value="8080" pattern="\d\d+" class="grid-item-col-2">
                <span class="grid-item-col-3 mw-50 ta-justified">Used for liveliness and health checks, and also for the service port.</span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="image_liveliness_endpoint" class="grid-item-col-1">Image liveliness endpoint</label>
                <input type="text" id="image_liveliness_endpoint" required placeholder="/health/live" value="/health/live" class="grid-item-col-2">
                <span class="grid-item-col-3">The endpoint used to check that the container still lives.</span>
            </div>
            <div class="subgrid-whole-row-3-col mw-50 ta-justified">
                <label for="image_readiness_endpoint" class="grid-item-col-1">Image readiness endpoint</label>
                <input type="text" id="image_readiness_endpoint" required placeholder="/health/ready" value="/health/ready" class="grid-item-col-2">
                <span class="grid-item-col-3">The endpoint used to check that the container is ready.</span>
            </div>
        </fieldset>
        <fieldset class="grid-parent col-gap-m row-gap-sm">
            <legend>Ingresses</legend>
            <div class="subgrid-whole-row-3-col">
                <label class="grid-item-col-1" for="ingress_fqdn">Address</label>
                <input class="grid-item-col-2" type="text" id="ingress_fqdn" placeholder="myservice.example.com" value="placeholder.example.com">
                <span class="grid-item-col-3"></span>
            </div>
            <div class="subgrid-whole-row-3-col">
                <label for="ingress_enable_oauth2proxy" class="grid-item-col-1">Add Oauth2Proxy</label>
                <input type="checkbox" id="ingress_enable_oauth2proxy" class="grid-item-col-2 justify-self-start">
                <span class="grid-item-col-3">Adds an <a href="https://oauth2-proxy.github.io/oauth2-proxy/">Oauth2Proxy</a> argo app deployment definition.</span>
            </div>
        </fieldset>
        ...
    </form>
</body>
<style>
    a, a:visited, a:hover, a:active {
        color: blue;
    }
    .todo {
        background-color: hotpink;
    }
    input:invalid {
        border: 2px dashed red;
        background-color: pink;
    }

    textarea:invalid {
        border: 2px dashed red;
        background-color: pink;
    }

    .mw-50 {
      max-width: 50rem;
    }

    .ta-justified {
      text-align: justify;
    }

    .monospace {
        font-family: 'Courier New', Courier, monospace;
    }
    .break-anywhere {
      word-break: break-all;
    }
    .bg-grey {
        background-color: lightgray;
    }
    .ts-small {
        font-size: small;
    }

    .grid-parent {
        display: grid;
        justify-content: start;
    }
    .subgrid-whole-row-3-col {
        display: grid;
        grid-template-columns: subgrid;
        grid-column: span 3;
        align-items: start;
    }
    .grid-item-col-1 {
        grid-column: 1;
    }
    .grid-item-col-2 {
        grid-column: 2;
    }
    .grid-item-col-3 {
        grid-column: 3;
    }
    .justify-self-start {
      justify-self: start;
    }
    .row-gap-sm {
        row-gap: 0.75rem;
    }
    .col-gap-m {
        column-gap: 1rem;
    }
    .col-gap-sm {
      column-gap: 0.5rem;
    }
</style>
<script type="text/javascript">/*Functionality*/</script>
</html>
```
