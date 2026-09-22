FROM alpine:3.22

RUN apk update && \
    apk add --no-cache go=$(cat .go-version)

WORKDIR /app
COPY . .
RUN go build ./...