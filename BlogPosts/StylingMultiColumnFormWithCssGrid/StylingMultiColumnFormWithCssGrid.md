Styling multi column forms using CSS grid
===

Sometimes we want to automate a process based on some simple user provided information. Often, this information is easy for the user to enter into a form, and for us to extract from the form. Especially if there is need for guidance about allowed values or formatting.

Another nice thing about forms, is that we can provide guidance beyond just failing the form. Like linking to documentation, or stating validation rules and giving examples instead of just saying that the value is unacceptable (looking at you, password reset forms).

Design wise, a mult column layout is a simple way to achieve this without having to make the user target tiny extra info icons or going deep into making text tiny and slightly more faded but not unreadable or breaking the form flow in general. So our question for this blog post becomes, how can we use a minimum of styling to make a simple and usable form with 3 columns:

1. Labels
2. Input fields
3. Descriptions/help

We would probably also like to make the browser handle most of the layout and sizing, so that we can focus on which fields we want, how the user should use them, and extracting what we need from them. Another concern we want to take care of is that not all groups of inputs/fields neccessarily need the same sizing and spacing.

With that in mind, let's get started!

*****

We start out with wrapping grouped items with shared column sizes in same parent element. On the parent we use the `display: grid` style to get the browsers to do as much as possible of the row and column placement and size handling. To make the input fields be placed as close as possible to their labels while still remaining neatly in row, we add the `justify-content: start` styling to the wrapper.

The CSS for this will look more or less like this:

```css
.grid-parent {
    display: grid;
    justify-content: start;
}
```

To use it we can have a simple collection of html items like shown below:

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

Now that we have our wrapper, we can make rows share grid column sizes, by making them subgrids. To do this the row content wrappers need to have style `display: grid` themselves, and also `grid-template-columns: subgrid`.

To make it all be the 3 columns that we want, we need to set the `grid-column` style. Our options are either to tell it to start the items at column 1 and end after 3/before column 4 with using `grid-column: 1/4`. Or we can simply say it spans 3 like this: `grid-column: span 3`.

Should the description column end up big spanning multiple rows, we can prevent input fields being weirdly vertically stretched, by also setting `align-items: start`.

So then we end up with the CSS below:

```css
.subgrid-whole-row-3-col {
    display: grid;
    grid-template-columns: subgrid;
    grid-column: span 3;
    align-items: start;
}
```

With the usage being like this:

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

Next we want to put each column item in its designated column. The style to use is `grid-column`, with the value being either just column number, or `start at/end before` like `grid-column: 1/2` if we want it to begin filling the first column and stop before/not fill the second column.

This makes our new CSS classes:

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

which we can apply like so:

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

Some elements, like checkboxes, misbehave and awkwardly place them selves in the middle of their column. To counter this, we add `justify-self` and set it to `start`.

```css
.justify-self-start {
    justify-self: start;
}
```

Applying it like show here:

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

Our form is beginning to come into shape, but depending on what we stick in it some breathing room can make it easier and more pleasant to navigate visually. To achieve this we can add some basic styling to parent grids. For the columns we can use the `column-gap` CSS property to make the fields and descriptions not be mashed together on a row. And to make it a bit easier to follow the rows and separate them from each other, the CSS property `row-gap` can be used for good effect.

As an example we can make CSS classes like:

```css
.row-gap {
    row-gap: 0.75rem;
}
.col-gap {
    column-gap: 1rem;
}
```

And use them as shown here:

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

A bonus remark regarding the description column. To make the text look nice and squared into our grid, we can use the CSS property `text-align: justify;`. Unfortunately, this occationally can produce some weird spacing between words even if you explicitly set the `hyphens: auto;` property, because the browser doesn't know/want to break shorter words or longer words where it makes sense. A simple fix is to insert `&shy;` soft hyphen markers in strategic places in the html markup, so that the browser knows to make an attempt at breaking words in those places with adding a hyphen, when it size wise makes sense (e.g. `in&shy;stead`). Note that the old `<wbr>` word break tag will not cause hyphens to be inserted. To handle long words where you give up, or code blocks where you don't want hyphens to be inserted should anyone copy the text, you can wrap it and apply the CSS property `word-break: break-all;` to the wrapper.

Finally then, a working example is shown below, or you can open [WorkingExample](./WorkingExample.html)

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
                <textarea id="image_repository" required name="image_repository" rows="1" cols="55" placeholder="container-registry.example.com/..." class="grid-item-col-2">container-registry.example.com/project-x/repository-a</textarea>
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
</html>
```
