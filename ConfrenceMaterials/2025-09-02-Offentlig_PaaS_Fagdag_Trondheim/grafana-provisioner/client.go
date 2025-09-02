package main

import (
	"github.com/go-openapi/strfmt"
	"github.com/grafana/grafana-openapi-client-go/client"
	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
	"net/http"
	"net/url"
	"time"
)

func newGrafanaClient() *client.GrafanaHTTPAPI {
	return client.NewHTTPClientWithConfig(strfmt.Default, &client.TransportConfig{
		Host:      grafanaUrl,
		Schemes:   []string{"http"},
		BasePath:  "/api",
		BasicAuth: url.UserPassword(grafanaUsername, grafanaPassword),
		Client:    &http.Client{Timeout: time.Second * 10, Transport: otelhttp.NewTransport(http.DefaultTransport)},
	})
}
