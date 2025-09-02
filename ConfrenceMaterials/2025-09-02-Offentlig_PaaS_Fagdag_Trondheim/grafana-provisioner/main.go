package main

import (
	"context"
	"errors"
	"github.com/go-openapi/strfmt"
	"github.com/grafana/grafana-openapi-client-go/client"
	"go.opentelemetry.io/contrib/bridges/otelslog"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/metric"
	"go.opentelemetry.io/otel/trace"
	"log"
	"log/slog"
	"os"
	"os/signal"
)

var (
	serviceName           = "grafana-provisioner"
	deploymentEnvironment = "Development"
	grafanaUrl            = "localhost:3000"
	grafanaUsername       = "admin"
	grafanaPassword       = "admin"
	logger                *slog.Logger
	meter                 metric.Meter
	tracer                trace.Tracer
	grafanaClient         = client.NewHTTPClient(strfmt.Default)
	cfg                   *config
)

func init() {
	value, ok := os.LookupEnv("SERVICE_NAME")
	if ok {
		serviceName = value
	}
	value, ok = os.LookupEnv("DEPLOYMENT_ENVIRONMENT")
	if ok {
		deploymentEnvironment = value
	}
	value, ok = os.LookupEnv("GRAFANA_URL")
	if ok {
		grafanaUrl = value
	}
	value, ok = os.LookupEnv("GRAFANA_USERNAME")
	if ok {
		grafanaUsername = value
	}
	value, ok = os.LookupEnv("GRAFANA_PASSWORD")
	if ok {
		grafanaPassword = value
	}
}

func main() {
	err := run()
	if err != nil {
		log.Fatalln(err)
	}
}

func run() error {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()

	shutdown, err := SetupOpenTelemetry(ctx)
	if err != nil {
		return err
	}
	defer func() {
		err = errors.Join(err, shutdown(context.Background()))
	}()

	logger = otelslog.NewLogger(serviceName)
	meter = otel.Meter(serviceName)
	tracer = otel.Tracer(serviceName)

	c, err := readConfig()
	if err != nil {
		return err
	}
	cfg = c

	grafanaClient = newGrafanaClient()

	provision()

	return nil
}
