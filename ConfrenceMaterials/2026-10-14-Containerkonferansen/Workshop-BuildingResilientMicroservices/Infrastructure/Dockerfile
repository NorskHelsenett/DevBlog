FROM docker:cli
RUN apk update
RUN apk add --no-cache \
        bash \
        ca-certificates \
        curl \
        git \
        helm \
        jq \
        yq \
        tar \
        go \
        gzip \
        kubectl \
        openssl \
        sed \
        gawk \
        bash-completion \
        gettext \
        libc6-compat \
        && update-ca-certificates

ENV GOROOT /usr/lib/go
ENV GOPATH /go
ENV PATH /go/bin:$PATH

RUN go install sigs.k8s.io/kind@latest

WORKDIR /work

ENTRYPOINT ["/bin/bash"]
