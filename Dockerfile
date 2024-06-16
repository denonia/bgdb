FROM rust:1.78.0 as builder

WORKDIR /usr/src/app

COPY . .
WORKDIR web
RUN cargo build --release --bin web

CMD ["cargo", "run", "--release", "--bin", "web"]
